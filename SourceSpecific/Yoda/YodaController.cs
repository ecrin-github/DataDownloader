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
        ScrapingBrowser browser;
        YodaDataLayer yoda_repo;
        Yoda_Processor processor;
        Source source;		
        string file_base;
        FileWriter file_writer;
        int saf_id;
        int source_id;
        LoggingDataLayer logging_repo;
        int? days_ago;

        public Yoda_Controller(ScrapingBrowser _browser, int _saf_id, Source _source, Args args, LoggingDataLayer _logging_repo)
        {
            browser = _browser;
            yoda_repo = new YodaDataLayer();
            processor = new Yoda_Processor();
            source = _source;
            file_base = source.local_folder;
            source_id = source.id;
            saf_id = _saf_id;
            file_writer = new FileWriter(source);
            logging_repo = _logging_repo;
            days_ago = args.skip_recent_days;
        }

        public DownloadResult LoopThroughPages()
        {
            // For Yoda, all data is downloaded each time during a download, as it takes a relatively short time
            // and the files simply replaced or - if new - added to the folder. There is therefore not a concept of an
            // update or focused download, as opposed to a full download.

            // set up initial study list.
            ScrapingHelpers ch = new ScrapingHelpers(browser, logging_repo);
            DownloadResult res = new DownloadResult();
            XmlSerializer writer = new XmlSerializer(typeof(Yoda_Record));
            List<Summary> all_study_list = new List<Summary>();
            string baseURL = "https://yoda.yale.edu/trials-search?amp%3Bpage=0&page=";
            //for (int i = 0; i < 4; i++)

            int search_page_limit;
            WebPage firstPage = ch.GetPage(baseURL + "0");
            if (firstPage == null)
            {
                logging_repo.LogError("Attempt to access first Yoda studies list page failed");
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
                    logging_repo.LogError("Unable to access record count total on first search page");
                    return null;
                }
            }

            int search_page_failures = 0;
            StringHelpers sh = new StringHelpers(logging_repo);
            for (int i = 0; i < search_page_limit; i++)
            {
                WebPage searchPage = ch.GetPage(baseURL + i.ToString());
                if (searchPage == null)
                {
                    logging_repo.LogError("Attempt to access Yoda studies list page (" + i.ToString() + " failed");
                    search_page_failures++;
                    return null;
                }
                else
                {
                    List<Summary> page_study_list = processor.GetStudyInitialDetails(searchPage, i, sh);
                    all_study_list.AddRange(page_study_list);

                    logging_repo.LogLine(i.ToString());
                    System.Threading.Thread.Sleep(300);
                }
            }
            if (search_page_failures > 0)
            {
                // do not proceed - give a null download result
                return null;
            }


            // Do a check on any possible id duplicates. Consider each study in turn.

            int n = 0;
            foreach (Summary sm in all_study_list)
            {
                string this_id = sm.sd_sid;
                n++;
                int id_num = 0;
                foreach (Summary s in all_study_list)
                {
                    if (this_id == s.sd_sid)
                    {
                        id_num++;
                    }
                }
                if (id_num > 1)
                {
                    logging_repo.LogError("More than one id found for " + n.ToString() + ": " + sm.study_name);
                }
            }


            foreach (Summary sm in all_study_list)
            {
                // Get the details page...
                // if record already downloaded today, ignore it... (may happen if re-running after an error)
                if (days_ago == null || !logging_repo.Downloaded_recently(source_id, sm.sd_sid, (int)days_ago))
                {
                    WebPage studyPage = ch.GetPage(sm.details_link);
                    res.num_checked++;

                    // Send the page off for processing

                    HtmlNode page = studyPage.Find("div", By.Class("region-content")).FirstOrDefault();
                    Yoda_Record st = processor.GetStudyDetails(ch, yoda_repo, page, sm, logging_repo);

                    if (st != null)
                    {
                        // Write out study record as XML.

                        string file_name = st.sd_sid + ".xml";
                        string full_path = Path.Combine(file_base, file_name);
                        file_writer.WriteYodaFile(writer, st, full_path);
                        bool added = logging_repo.UpdateStudyDownloadLog(source_id, st.sd_sid, st.remote_url, saf_id,
                                                          st.last_revised_date, full_path);
                        res.num_downloaded++;
                        if (added) res.num_added++;

                        // Put a pause here if necessary.

                        System.Threading.Thread.Sleep(500);
                    }

                    logging_repo.LogLine(res.num_checked.ToString());
                }
            }

            return res;
        }
    }
}
