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
            // if sf_type = 145 only download files 
            // with a download status of 0

            ScrapingHelpers ch = new ScrapingHelpers(browser, logging_repo);
            bool do_all_records = sf_type_id != 145;
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
                // i needs to start at a high figure to miss oput all the non-changed
                // records - changed records appear to be at the 'high' end of the
                // database / web pages. i = 1800 skips the first 36,000 entries!
                // (will need changing if logs indicate this is problematic)

                int loop_limit = rec_num % 20 == 0 ? rec_num / 20 : (rec_num / 20) + 1;
                for (int i = 1800; i <= loop_limit; i++)
                {
                    System.Threading.Thread.Sleep(100);

                    // Go to the summary page indicated by current value of i
                    // Each page has up to 20 listed studies.

                    searchPage = ch.GetPage(baseURL + i.ToString());
                    if (searchPage != null)
                    {
                        // first get a simple list of EUCTR ids from that page
                        // and check then against the database (unless downloading everything)

                        List<EUCTR_Summmary> summaries = processor.GetStudyList(searchPage);
                        int num_to_download = 0;
                        foreach (EUCTR_Summmary s in summaries)
                        {
                            if (do_all_records)
                            {
                                s.do_download = true;
                                num_to_download++;
                            }
                            else
                            {
                                // Check the euctr_id (sd_id) record
                                StudyFileRecord file_record = logging_repo.FetchStudyFileRecord(s.eudract_id, source_id);
                                if (file_record == null || file_record.download_status == 0)
                                {
                                    s.do_download = true;  // only download if DL status = 0
                                    num_to_download++;
                                }
                            }
                            res.num_checked++;
                        }

                        if (num_to_download > 0)
                        {
                            // only proceed if there are any studies to download on this page
                            // First, get full details into the summaries
                            string summary_fill_result = processor.GetFullSummaries(searchPage, summaries);
                            if (summary_fill_result != "OK")
                            {
                                logging_repo.LogError(summary_fill_result);
                                return res;
                            }

                            // now use 'filled' summaries as before
                            foreach (EUCTR_Summmary s in summaries)
                            {
                                if (s.do_download)
                                {
                                    if (days_ago != null && logging_repo.Downloaded_recently(source_id, s.eudract_id, (int)days_ago))
                                    {
                                        s.do_download = false; 
                                    }
                                }

                                if (s.do_download)
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

                                        // Get the source data record and modify it or add a new one...
                                        StudyFileRecord file_record = logging_repo.FetchStudyFileRecord(st.eudract_id, source_id);
                                        
                                        if (file_record == null)
                                        {
                                            // should always be present but just in case...    
                                            // this neeeds to have a new record
                                            file_record = new StudyFileRecord(source_id, st.eudract_id, st.details_url, 
                                                                              saf_id, null, full_path);
                                            logging_repo.InsertStudyFileRec(file_record);
                                            res.num_added++;
                                        }
                                        else
                                        {
                                            // update record
                                            file_record.remote_url = st.details_url;
                                            file_record.last_saf_id = saf_id;
                                            file_record.download_status = 2;
                                            file_record.last_downloaded = DateTime.Now;
                                            file_record.local_path = full_path;

                                            // Update file record
                                            logging_repo.StoreStudyFileRec(file_record);
                                        }

                                        res.num_downloaded++;
                                    }
                                    else
                                    {
                                        logging_repo.LogError("Problem in navigating to protocol details, id is " + s.eudract_id);
                                        CheckAccessErrorCount();
                                    }

                                    System.Threading.Thread.Sleep(500);
                                }
                            }

                        }

                    }
                    else
                    {
                        logging_repo.LogError("Problem in navigating to summary page, page value is " + i.ToString());
                        CheckAccessErrorCount();
                    }

                    if (res.num_checked % 100 == 0)
                    {
                        logging_repo.LogLine("EUCTR pages checked: " + res.num_checked.ToString());
                        logging_repo.LogLine("EUCTR pages downloaded: " + res.num_downloaded.ToString());
                    }
                }
            }

            logging_repo.LogLine("Number of errors: " + access_error_num.ToString());
            return res;
        }


        private void CheckAccessErrorCount()
        {
            access_error_num++;
            if (access_error_num % 5 == 0)
            {
                // do a 5 minute pause
                TimeSpan pause = new TimeSpan(0, 1, 0);
                System.Threading.Thread.Sleep(pause);
                pause_error_num++;
                access_error_num = 0;
            }
            if (pause_error_num > 5)
            {
                //throw new Exception("Too many access errors");
            }
        }
        
    }
}

/*
 *      OLD CODE using 'assumed complete' and the search pages
 *      to go through all records....
 *      
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

            int skipped = 0;
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
                            if (!do_download) skipped++;

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

                            if (res.num_checked % 10 == 0)
                            {
                                logging_repo.LogLine("EUCTR pages checked: " + res.num_checked.ToString());
                                logging_repo.LogLine("EUCTR pages skipped: " + skipped.ToString());
                            }
                        }

                    }
                    else
                    {
                        logging_repo.LogError("Problem in navigating to summary page, page value is " + i.ToString());
                        CheckAccessErrorCount();
                    }
                }
            }

            logging_repo.LogLine("Number of errors: " + access_error_num.ToString());
            return res;
        }


*/
