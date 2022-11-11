using HtmlAgilityPack;
using ScrapySharp.Html;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace DataDownloader.yoda
{
    class Yoda_Controller
    {
        ScrapingBrowser _browser;
        YodaDataLayer _yoda_repo;
        Yoda_Processor _processor;
        Source _source;		
        string _file_base;
        FileWriter _file_writer;
        int _saf_id;
        int _source_id;
        int? _days_ago;        
        MonitorDataLayer _monitor_repo;
        LoggingHelper _logging_helper;


        public Yoda_Controller(ScrapingBrowser browser, int saf_id, Source source, Args args, MonitorDataLayer monitor_repo, LoggingHelper logging_helper)
        {
            _browser = browser;
            _yoda_repo = new YodaDataLayer();
            _processor = new Yoda_Processor();
            _source = source;
            _file_base = source.local_folder;
            _source_id = source.id;
            _saf_id = saf_id;
            _file_writer = new FileWriter(source);
            _monitor_repo = monitor_repo;
            _logging_helper = logging_helper;
            _days_ago = args.skip_recent_days;
        }

        public DownloadResult LoopThroughPages()
        {
            // For Yoda, all data is downloaded each time during a download, as it takes a relatively short time
            // and the files simply replaced or - if new - added to the folder. There is therefore not a concept of an
            // update or focused download, as opposed to a full download.

            // set up initial study list.
            ScrapingHelpers ch = new ScrapingHelpers(_browser, _logging_helper);
            DownloadResult res = new DownloadResult();
            XmlSerializer writer = new XmlSerializer(typeof(Yoda_Record));
            List<Summary> all_study_list = new List<Summary>();
            string baseURL = "https://yoda.yale.edu/trials-search?amp%3Bpage=0&page=";
            //for (int i = 0; i < 4; i++)

            int search_page_limit;
            WebPage firstPage = ch.GetPage(baseURL + "0");
            if (firstPage == null)
            {
                _logging_helper.LogError("Attempt to access first Yoda studies list page failed");
                return null;
            }
            else
            {
                // Find total number of studies listed.

                var pageContent = firstPage.Find("div", By.Class("result-count")).FirstOrDefault();
                string record_string = pageContent.InnerText;
                int of_pos = record_string.IndexOf(" of ");
                string record_number_string = record_string.Substring(of_pos + 4);
                if (Int32.TryParse(record_number_string, out int record_count))
                {
                    search_page_limit = (record_count % 10 == 0) ? record_count / 10 : (record_count / 10) + 1;
                }
                else
                {
                    search_page_limit = 0;
                    _logging_helper.LogError("Unable to access record count total on first search page");
                    return null;
                }
            }

            int search_page_failures = 0;
            for (int i = 0; i < search_page_limit; i++)
            {
                WebPage searchPage = ch.GetPage(baseURL + i.ToString());
                if (searchPage == null)
                {
                    _logging_helper.LogError("Attempt to access Yoda studies list page (" + i.ToString() + " failed");
                    search_page_failures++;
                    return null;
                }
                else
                {
                    List<Summary> page_study_list = _processor.GetStudyInitialDetails(searchPage, i);
                    all_study_list.AddRange(page_study_list);

                    _logging_helper.LogLine("search page: " + i.ToString());
                    System.Threading.Thread.Sleep(300);
                }
            }
            if (search_page_failures > 0)
            {
                // do not proceed - give a null download result
                return null;
            }

            // Do a check on any possible id duplicates. Consider each study in turn.
            // Duplicates rare but do occur (1 at least with two Yoda entries pointing to the sam page)

            int n = 0;
            List<Summary> study_list = new List<Summary>();

            foreach (Summary sm in all_study_list)
            {
                n++;
                bool transfer_to_list = true;
                                
                string id_to_check = sm.sd_sid;
                int s_pos = 0;
                foreach (Summary s in study_list)
                {
                    s_pos++;
                    if (id_to_check == s.sd_sid)
                    {
                        _logging_helper.LogLine("More than one id found for " + n.ToString() + ": " + sm.study_name);
                        transfer_to_list = false;
                    }
                }
                
                if (transfer_to_list)
                {
                    study_list.Add(sm);
                }
            }


            foreach (Summary sm in study_list)
            {
                // Get the details page...
                // if record already downloaded today, ignore it... (may happen if re-running after an error)
                if (_days_ago == null || !_monitor_repo.Downloaded_recently(_source_id, sm.sd_sid, (int)_days_ago))
                {
                    WebPage studyPage = ch.GetPage(sm.details_link);
                    res.num_checked++;

                    // Send the page off for processing

                    HtmlNode page = studyPage.Find("div", By.Class("region-content")).FirstOrDefault();
                    Yoda_Record st = _processor.GetStudyDetails(ch, _yoda_repo, page, sm, _logging_helper);

                    if (st != null)
                    {
                        // Write out study record as XML.

                        string file_name = st.sd_sid + ".xml";
                        string full_path = Path.Combine(_file_base, file_name);
                        _file_writer.WriteYodaFile(writer, st, full_path);
                        bool added = _monitor_repo.UpdateStudyDownloadLog(_source_id, st.sd_sid, st.remote_url, _saf_id,
                                                          st.last_revised_date, full_path);
                        res.num_downloaded++;
                        if (added) res.num_added++;

                        // Put a pause here if necessary.

                        System.Threading.Thread.Sleep(500);
                    }

                    _logging_helper.LogLine(res.num_checked.ToString());
                }
            }

            return res;
        }
    }
}
