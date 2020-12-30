using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace DataDownloader.euctr
{
    class EUCTR_Controller
    {
        ScrapingBrowser browser;
        EUCTR_Processor processor;
        Source source;
        FileWriter file_writer;
        string file_base;
        int saf_id;
        int source_id;
        int sf_type_id;
        LoggingDataLayer logging_repo;
        int? days_ago;
        int access_error_num = 0;
        int pause_error_num = 0;

        public EUCTR_Controller(ScrapingBrowser _browser, int _saf_id, Source _source, Args args, LoggingDataLayer _logging_repo)
        {
            browser = _browser;
            processor = new EUCTR_Processor();
            source = _source;
            file_base = source.local_folder;
            source_id = source.id;
            saf_id = _saf_id;
            file_writer = new FileWriter(source);
            logging_repo = _logging_repo;
            sf_type_id = args.type_id;
            days_ago = args.skip_recent_days;
        }


        public DownloadResult LoopThroughPages()
        {
            // consider type - from args
            // if sf_type = 141, 142, 143 (normally 142 here) only download files 
            // not already marked as 'complete' - i.e. very unlikely to change. This is 
            // signalled by including the flag in the call to the processor routine.

            ScrapingHelpers ch = new ScrapingHelpers(browser, logging_repo);
            bool do_all_records = !(sf_type_id == 141 || sf_type_id == 142 || sf_type_id == 143);
            DownloadResult res = new DownloadResult();
            XmlSerializer writer = new XmlSerializer(typeof(EUCTR_Record));
            string baseURL = "https://www.clinicaltrialsregister.eu/ctr-search/search?query=&page=";
            WebPage searchPage = ch.GetPage(baseURL);
            if (searchPage == null)
            {
                // Appears as if web site completely down
                return null;
            }

            int rec_num = processor.GetListLength(searchPage);
            if (rec_num != 0)
            {
                int loop_limit = rec_num % 20 == 0 ? rec_num / 20 : (rec_num / 20) + 1;
                for (int i = 0; i <= loop_limit; i++)
                {
                    // Go to the summary page indicated by current value of i
                    // Each page has up to 20 listed studies.
                    // Once on that page each of the studies is processed in turn...
                    searchPage = ch.GetPage(baseURL + i.ToString());
                    if (searchPage != null)
                    {
                        List<EUCTR_Summmary> summaries = processor.GetStudySuummaries(searchPage);

                        foreach (EUCTR_Summmary s in summaries)
                        {
                            // Check the euctr_id (sd_id) is not 'assumed complete' if only incomplete 
                            // records are being considered; only proceed if this is the case
                            bool do_download = false;
                            res.num_checked++;

                            StudyFileRecord file_record = logging_repo.FetchStudyFileRecord(s.eudract_id, source_id);
                            if (file_record == null)
                            {
                                do_download = true;  // record does not yet exist
                            }
                            else if (do_all_records || file_record.assume_complete != true)
                            {
                                // if record exists only consider it if the 'incomplete only' flag is being ignored,
                                // or the completion status is false or null
                                // Even then do a double check to ensure the record has not been recently downloaded
                                if (days_ago == null || !logging_repo.Downloaded_recently(source_id, s.eudract_id, (int)days_ago))
                                {
                                    do_download = true; // download if not assumed complete, or incomplete only flag does not apply
                                }
                            }

                            if (do_download)
                            {
                                // transfer summary details to the main EUCTR_record object
                                EUCTR_Record st = new EUCTR_Record(s);

                                WebPage detailsPage = ch.GetPage(st.details_url);
                                if (detailsPage != null)
                                {
                                    st = processor.ExtractProtocolDetails(st, detailsPage);

                                    // Then get results details

                                    if (st.results_url != null)
                                    {
                                        System.Threading.Thread.Sleep(800);
                                        WebPage resultsPage = ch.GetPage(st.results_url);
                                        if (resultsPage != null)
                                        {
                                            st = processor.ExtractResultDetails(st, resultsPage);
                                        }
                                        else
                                        {
                                            logging_repo.LogError("Problem in navigating to result details, id is " + s.eudract_id);
                                        }
                                    }

                                    // Write out study record as XML.
                                    if (!Directory.Exists(file_base))
                                    {
                                        Directory.CreateDirectory(file_base);
                                    }
                                    string file_name = "EU " + st.eudract_id + ".xml";
                                    string full_path = Path.Combine(file_base, file_name);
                                    file_writer.WriteEUCTRFile(writer, st, full_path);

                                    bool assume_complete = false;
                                    if (st.trial_status == "Completed" && st.results_url != null)
                                    {
                                        assume_complete = true;
                                    }
                                    bool added = logging_repo.UpdateStudyDownloadLogWithCompStatus(source_id, st.eudract_id,
                                                                       st.details_url, saf_id,
                                                                       assume_complete, full_path);
                                    res.num_downloaded++;
                                    if (added) res.num_added++;
                                }
                                else
                                {
                                    logging_repo.LogError("Problem in navigating to protocol details, id is " + s.eudract_id);
                                    CheckAccessErrorCount();
                                }

                                System.Threading.Thread.Sleep(800);
                            }

                            if (res.num_checked % 10 == 0) logging_repo.LogLine("EUCTR pages checked: " + res.num_checked.ToString());
                        }

                    }
                    else
                    {
                        logging_repo.LogError("Problem in navigating to summary page, page value is " + i.ToString());
                        CheckAccessErrorCount();
                    }
                }
            }

            return res;
        }


        private void CheckAccessErrorCount()
        {
            access_error_num++;
            if (access_error_num > 5)
            {
                // do a 15 minute pause
                TimeSpan pause = new TimeSpan(0, 15, 0);
                System.Threading.Thread.Sleep(pause);
                pause_error_num++;
                access_error_num = 0;
            }
            if (pause_error_num > 5)
            {
                throw new Exception("Too many access errors");
            }
        }
        
    }
}
