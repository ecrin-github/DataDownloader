using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Html;
using ScrapySharp.Network;
using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace DataDownloader.isrctn
{
    class ISRCTN_Controller
    {
        ScrapingBrowser _browser;
        ISRCTN_Processor _processor;
        Source _source;
        string _file_base;
        FileWriter _file_writer;
        int _saf_id;
        int _source_id;
        DateTime? _cut_off_date;
        string _cut_off_date_string;
        int? _days_ago;
        MonitorDataLayer _monitor_repo;
        LoggingHelper _logging_helper;

        public ISRCTN_Controller(ScrapingBrowser browser, int saf_id, Source source, Args args, MonitorDataLayer monitor_repo, LoggingHelper logging_helper)
        {
            _browser = browser;
            _processor = new ISRCTN_Processor();
            _source = source;
            _file_base = source.local_folder;
            _source_id = source.id;
            _saf_id = saf_id;
            _file_writer = new FileWriter(source);
            _monitor_repo = monitor_repo;
            _logging_helper = logging_helper;
            _cut_off_date = args.cutoff_date;
            _cut_off_date_string = args.cutoff_date?.ToString("yyyy-MM-dd");
            _days_ago = args.skip_recent_days;
        }

        // Get the number of records required and set up the loop

        public DownloadResult LoopThroughPages()
        {
            // Construct the initial search string
            DownloadResult res = new DownloadResult();
            XmlSerializer writer = new XmlSerializer(typeof(ISCTRN_Record));
            ScrapingHelpers ch = new ScrapingHelpers(_browser, _logging_helper);
            

            string part1_of_url = "http://www.isrctn.com/search?pageSize=100&page=";
            string part2_of_url = "&q=&filters=GT+lastEdited%3A";
            string end_of_url =   "T00%3A00%3A00.000Z&searchType=advanced-search";

            string start_url = part1_of_url + "1" + part2_of_url + _cut_off_date_string + end_of_url;
            WebPage prePage = ch.GetPage(start_url);   // throw away the initial page received - gives a 'use API' message
            WebPage summaryPage = ch.GetPage(start_url);

            int rec_num = _processor.GetListLength(summaryPage);  // unable to scrape the summary page
            
            if (rec_num != 0)
            {
                int loop_limit = rec_num % 100 == 0 ? rec_num / 100 : (rec_num / 100) + 1;

                for (int i = 1; i <= loop_limit; i++)
                {
                    // Obtain and go through each page of up to 100 entries.

                    summaryPage = ch.GetPage(part1_of_url + i.ToString() + 
                                          part2_of_url + _cut_off_date_string + end_of_url);

                    var studies = _processor.GetPageStudyList(summaryPage);

                    foreach (ISCTRN_Study s in studies)
                    {
                        int n = 0;
                        
                        string study_id = s.ISRCTN_number;

                        // record has been added or revised since the cutoff date (normally the last download), 
                        // but...should it be downloaded

                        bool do_download = true;  // by default
                        if (_days_ago != null)
                        {
                            // but if there was a recent download of the same file
                            // do not download again

                            if (_monitor_repo.Downloaded_recently(_source_id, study_id, (int)_days_ago))
                            {
                                do_download = false;
                            }
                        }
                        res.num_checked++;

                        if (do_download)
                        { 
                            string link = s.remote_link;

                            // obtain details of that study
                            // but pause every 5 accesses
                            n++;
                            if (n % 5 == 0)
                            {
                                System.Threading.Thread.Sleep(2000);
                            }
                            ISCTRN_Record st = new ISCTRN_Record();
                            WebPage detailsPage = ch.GetPage(s.remote_link);
                            st = _processor.GetFullDetails(detailsPage, study_id);

                            // Write out study record as XML.
                            if (!Directory.Exists(_file_base))
                            {
                                Directory.CreateDirectory(_file_base);
                            }
                            string file_name = st.isctrn_id + ".xml";
                            string full_path = Path.Combine(_file_base, file_name);

                            _file_writer.WriteISRCTNFile(writer, st, full_path);
                            bool added = _monitor_repo.UpdateStudyDownloadLog(_source_id, study_id, s.remote_link, _saf_id,
                                                                st.last_edited, full_path);
                            res.num_downloaded++;
                            if (added) res.num_added++;
                        }
                    }

                    _logging_helper.LogLine(res.num_checked.ToString() + " files checked");
                    _logging_helper.LogLine(res.num_downloaded.ToString() + " files downloaded");
                }
              
            }
            
            return res;
        }
    }
}
