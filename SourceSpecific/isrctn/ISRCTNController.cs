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
        ScrapingBrowser browser;
        ISRCTN_Processor processor;
        Source source;
        string file_base;
        FileWriter file_writer;
        int saf_id;
        int source_id;
        LoggingDataLayer logging_repo;
        DateTime? cut_off_date;
        string cut_off_date_string;
        int? days_ago;

        public ISRCTN_Controller(ScrapingBrowser _browser, int _saf_id, Source _source, Args args, LoggingDataLayer _logging_repo)
        {
            browser = _browser;
            processor = new ISRCTN_Processor();
            source = _source;
            file_base = source.local_folder;
            source_id = source.id;
            saf_id = _saf_id;
            file_writer = new FileWriter(source);
            logging_repo = _logging_repo;
            cut_off_date = args.cutoff_date;
            cut_off_date_string = args.cutoff_date?.ToString("yyyy-MM-dd");
            days_ago = args.skip_recent_days;
        }

        // Get the number of records required and set up the loop

        public DownloadResult LoopThroughPages()
        {
            // Construct the initial search string
            DownloadResult res = new DownloadResult();
            XmlSerializer writer = new XmlSerializer(typeof(ISCTRN_Record));
            ScrapingHelpers ch = new ScrapingHelpers(browser, logging_repo);
            

            string part1_of_url = "http://www.isrctn.com/search?pageSize=100&page=";
            string part2_of_url = "&q=&filters=GT+lastEdited%3A";
            string end_of_url =   "T00%3A00%3A00.000Z&searchType=advanced-search";

            string start_url = part1_of_url + "1" + part2_of_url + cut_off_date_string + end_of_url;
            WebPage prePage = ch.GetPage(start_url);   // throw away the initial page received - gives a 'use API' message
            WebPage summaryPage = ch.GetPage(start_url);

            int rec_num = processor.GetListLength(summaryPage);  // unable to scrape the summary page
            
            if (rec_num != 0)
            {
                int loop_limit = rec_num % 100 == 0 ? rec_num / 100 : (rec_num / 100) + 1;

                for (int i = 1; i <= loop_limit; i++)
                {
                    // Obtain and go through each page of up to 100 entries.

                    summaryPage = ch.GetPage(part1_of_url + i.ToString() + 
                                          part2_of_url + cut_off_date_string + end_of_url);

                    var studies = processor.GetPageStudyList(summaryPage);

                    foreach (ISCTRN_Study s in studies)
                    {
                        int n = 0;
                        
                        string study_id = s.ISRCTN_number;

                        // record has been added or revised since the cutoff date (normally the last download), 
                        // but...should it be downloaded

                        bool do_download = true;  // by default
                        if (days_ago != null)
                        {
                            // but if there was a recent download of the same file
                            // do not download again

                            if (logging_repo.Downloaded_recently(source_id, study_id, (int)days_ago))
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
                            st = processor.GetFullDetails(detailsPage, study_id);

                            // Write out study record as XML.
                            if (!Directory.Exists(file_base))
                            {
                                Directory.CreateDirectory(file_base);
                            }
                            string file_name = st.isctrn_id + ".xml";
                            string full_path = Path.Combine(file_base, file_name);

                            file_writer.WriteISRCTNFile(writer, st, full_path);
                            bool added = logging_repo.UpdateStudyDownloadLog(source_id, study_id, s.remote_link, saf_id,
                                                                st.last_edited, full_path);
                            res.num_downloaded++;
                            if (added) res.num_added++;
                        }
                    }

                    logging_repo.LogLine(res.num_checked.ToString() + " files checked");
                    logging_repo.LogLine(res.num_downloaded.ToString() + " files downloaded");
                }
              
            }
            
            return res;
        }
    }
}
