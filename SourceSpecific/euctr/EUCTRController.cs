using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace DataDownloader.euctr
{
    class EUCTR_Controller
    {
        ScrapingBrowser _browser;
        EUCTR_Processor _processor;
        Source _source;
        FileWriter _file_writer;
        string _file_base;
        int _saf_id;
        int _source_id;
        Args _args;
        int _access_error_num = 0;
        int _pause_error_num = 0;
        MonitorDataLayer _monitor_repo;
        LoggingHelper _logging_helper;

        public EUCTR_Controller(ScrapingBrowser browser, int saf_id, Source source, Args args, MonitorDataLayer monitor_repo, LoggingHelper logging_helper)
        {
            _browser = browser;
            _processor = new EUCTR_Processor();
            _source = source;
            _file_base = source.local_folder;
            _source_id = source.id;
            _saf_id = saf_id;
            _file_writer = new FileWriter(source);
            _args = args;
            _monitor_repo = monitor_repo;
            _logging_helper = logging_helper;
        }


        public DownloadResult LoopThroughPages()
        {

            // first ensure that the web site is up
            // and get total record numbers and total page numbers

            ScrapingHelpers ch = new ScrapingHelpers(_browser, _logging_helper);
            string baseURL = "https://www.clinicaltrialsregister.eu/ctr-search/search?page=";
            WebPage initialPage = ch.GetPage(baseURL + "1");

            if (initialPage != null)
            {
                // first get total number of records 
                // Only proceed if that initial task is possible

                int rec_num = _processor.GetListLength(initialPage);
                if (rec_num != 0)
                {
                    int page_num = rec_num % 20 == 0 ? rec_num / 20 : (rec_num / 20) + 1;

                    // if sf_type = 145 only download files with a download status of 0
                    // if type = 146 scrape all records in the designated page range (20 records per page)
                    // in both cases ignore records that have been downloaded in recent interval (I, 'skip recent' parameter)

                    if (_args.type_id == 145)
                    {
                        return LoopThrougRecsWithDLStatusZero(ch, baseURL, page_num, _args.skip_recent_days);
                    }
                    else if (_args.type_id == 146)
                    {
                        if (_args.start_page != null && _args.end_page != null)
                        {
                            int end_page = _args.end_page > page_num ? page_num : (int)_args.end_page;
                            return LoopThroughAllPages(ch, baseURL, (int)_args.start_page, end_page, _args.skip_recent_days);
                        }
                        else
                        {
                            return new DownloadResult("Valid start and end page numbers not provided for page based download");
                        }
                    }
                    else
                    {
                        return new DownloadResult("Download type requested not in allowed list for EU CTR");
                    }
                }
                else
                {
                    return new DownloadResult("Unable to capture total record numbers in preliminaryu set up, so unable to proceed");
                }
            }
            else
            {
                // Appears as if web site completely down
                return new DownloadResult("Unable to open initial summary page (web site may be down), so unable to proceed");
            }
        }


        public DownloadResult LoopThrougRecsWithDLStatusZero(ScrapingHelpers ch, string baseURL, int page_num, int? days_ago)
        {
            DownloadResult res = new DownloadResult();

            // i normally needs to start at a high figure to miss out all the non-changed
            // records - changed records appear to be at the 'high' end of the
            // database / web pages. i = 2000 skips the first 40,000 entries!
            // (will need changing if logs indicate this is problematic)

            for (int i = 1; i <= page_num; i++)
            {
                //if (res.num_downloaded > 5) break;  // for testing

                System.Threading.Thread.Sleep(100);

                // Go to the summary page indicated by current value of i
                // Each page has up to 20 listed studies.

                var summaryPage = ch.GetPage(baseURL + i.ToString());
                if (summaryPage != null)
                {
                    // first get a simple list of EUCTR ids from that page
                    // and check then against the database to see which ones really need downloading

                    List<EUCTR_Summmary> summaries = _processor.GetStudyList(summaryPage);
                    int num_to_download = 0;
                    foreach (EUCTR_Summmary s in summaries)
                    {
                        StudyFileRecord file_record = _monitor_repo.FetchStudyFileRecord(s.eudract_id, _source_id);
                        res.num_checked++;

                        if (file_record == null || file_record.download_status == 0)
                        {
                            // only download if DL status = 0 or record does not yet exist (unlikely in this case)

                            s.do_download = true;  // only download if DL status = 0, but...

                            if (days_ago != null)
                            {
                                if (_monitor_repo.Downloaded_recently(_source_id, s.eudract_id, (int)days_ago))
                                {
                                    s.do_download = false;
                                }
                            }

                            if (s.do_download) num_to_download++;
                        }
                    }

                    // only proceed if there are any studies to download on this page 
                    // First, get full details into the summaries
                    // (details only filled if download is required)

                    if (num_to_download > 0)
                    {
                        for (int j = 0; j < summaries.Count; j++)
                        {
                            if (summaries[j].do_download)
                            {
                                // get full details
                                try
                                {
                                    EUCTR_Summmary s = _processor.DeriveFullSummary(summaryPage, summaries[j], j);

                                    if (s.trial_status != null && s.trial_status.StartsWith("At position "))
                                    {
                                        // a problem with matching ids - stop the process
                                        res.error_message = s.trial_status;
                                        return res;
                                    }

                                    // OK to proceed -  transfer summary details to the main EUCTR_record object
                                    // and then get full protocol details

                                    EUCTR_Record st = new EUCTR_Record(s);

                                    WebPage detailsPage = ch.GetPage(st.details_url);
                                    if (detailsPage != null)
                                    {
                                        st = _processor.ExtractProtocolDetails(st, detailsPage);

                                        // Then get results details if available

                                        if (st.results_url != null)
                                        {
                                            System.Threading.Thread.Sleep(800);
                                            WebPage resultsPage = ch.GetPage(st.results_url);
                                            if (resultsPage != null)
                                            {
                                                st = _processor.ExtractResultDetails(st, resultsPage);
                                            }
                                            else
                                            {
                                                _logging_helper.LogError("Problem in navigating to result details, eudract_id is " + s.eudract_id);
                                                CheckAccessErrorCount();
                                            }
                                        }

                                        // Write out study record as XML.

                                        string full_path = WriteOutFile(st.eudract_id, st);

                                        // Update the source data record, modifying it or adding a new one...

                                        if (UpdateFileRecord(_source_id, st.eudract_id, st.details_url, full_path))
                                        {
                                            res.num_added++;
                                        }

                                        res.num_downloaded++;
                                    }
                                    else
                                    {
                                        _logging_helper.LogError("Problem in navigating to protocol details page, eudract_id is " + s.eudract_id);
                                        CheckAccessErrorCount();
                                    }

                                    System.Threading.Thread.Sleep(500);
                                }
                                catch (Exception e)
                                {
                                    //To do
                                    string s = e.Message;
                                }
                            }
                        }
                    }
                }
                else
                {
                    _logging_helper.LogError("Problem in navigating to summary page, page value is " + i.ToString());
                    CheckAccessErrorCount();
                }

                if (res.num_checked % 100 == 0)
                {
                    _logging_helper.LogLine("EUCTR pages checked: " + res.num_checked.ToString());
                    _logging_helper.LogLine("EUCTR pages downloaded: " + res.num_downloaded.ToString());
                }
            }

            _logging_helper.LogLine("Number of errors: " + _access_error_num.ToString());
            return res;
        }


        public DownloadResult LoopThroughAllPages(ScrapingHelpers ch, string baseURL, int spage, int epage, int? days_ago )
        {
            // loops through all summary pages in a given range
            // called using call parameters
            // that sets the page numbers to be traversed by this routine

            DownloadResult res = new DownloadResult();

            for (int i = spage; i <= epage; i++)
            {
                // if (res.num_downloaded > 2) break; // for testing

                System.Threading.Thread.Sleep(100);

                // Go to the summary page indicated by current value of i
                // Each page has up to 20 listed studies.

                WebPage summaryPage = ch.GetPage(baseURL + i.ToString());
                if (summaryPage != null)
                { 
                    List<EUCTR_Summmary> summaries = _processor.GetStudyList(summaryPage, true);

                    // by default all studies are scraped, but if the 'days ago' parameter has a value
                    // each study needs to be checked first, to see if it is still necessar=y to download it

                    if (days_ago != null)
                    {
                        for (int j = 0; j < summaries.Count; j++)
                        {
                            if (_monitor_repo.Downloaded_recently(_source_id, summaries[j].eudract_id, (int)days_ago))
                            {
                                summaries[j].do_download = false;
                            }
                        }
                    }
                    res.num_checked += 20;

                    for (int j = 0; j < summaries.Count; j++)
                    {
                        if (summaries[j].do_download)
                        {
                            EUCTR_Summmary s = _processor.DeriveFullSummary(summaryPage, summaries[j], j);
                            if (s.trial_status != null && s.trial_status.StartsWith("At position "))
                            {
                                // a problem with matching ids - stop the process
                                res.error_message = s.trial_status;
                                return res;
                            }

                            // OK to proceed -  transfer summary details to the main EUCTR_record object
                            // and then get full protocol details

                            EUCTR_Record st = new EUCTR_Record(s);

                            WebPage detailsPage = ch.GetPage(st.details_url);
                            if (detailsPage != null)
                            {
                                st = _processor.ExtractProtocolDetails(st, detailsPage);

                                // Then get results details

                                if (st.results_url != null)
                                {
                                    System.Threading.Thread.Sleep(800);
                                    WebPage resultsPage = ch.GetPage(st.results_url);
                                    if (resultsPage != null)
                                    {
                                        st = _processor.ExtractResultDetails(st, resultsPage);
                                    }
                                    else
                                    {
                                        _logging_helper.LogError("Problem in navigating to result details, eudract_id is " + s.eudract_id);
                                    }
                                }

                                // Write out study record as XML.

                                string full_path = WriteOutFile(st.eudract_id, st);

                                // Update the source data record, modifying it or adding a new one...

                                if (UpdateFileRecord(_source_id, st.eudract_id, st.details_url, full_path))
                                {
                                    res.num_added++;
                                }
                                res.num_downloaded++;

                            }
                            else
                            {
                                _logging_helper.LogError("Problem in navigating to protocol details, eudract_id is " + s.eudract_id);
                                CheckAccessErrorCount();
                            }

                            System.Threading.Thread.Sleep(500);
                        }
                    }
                }
                else
                {
                    _logging_helper.LogError("Problem in navigating to summary page, page value is " + i.ToString());
                    CheckAccessErrorCount();
                }

                if (res.num_checked % 20 == 0)
                {
                    _logging_helper.LogLine("EUCTR pages checked: " + res.num_checked.ToString());
                    _logging_helper.LogLine("EUCTR pages downloaded: " + res.num_downloaded.ToString());
                }
            }

            _logging_helper.LogLine("Number of errors: " + _access_error_num.ToString());
            return res;
        }


        private bool UpdateFileRecord(int source_id, string eudract_id, string details_url, string full_path)
        {
            // Get the source data record and modify it or add a new one...
            StudyFileRecord file_record = _monitor_repo.FetchStudyFileRecord(eudract_id, source_id);
            bool addition = false;

            if (file_record == null)
            {
                // should always be present but just in case...    
                // this neeeds to have a new record
                file_record = new StudyFileRecord(source_id, eudract_id, details_url,
                                                    _saf_id, null, full_path);
                _monitor_repo.InsertStudyFileRec(file_record);
                addition = true;
            }
            else
            {
                // update record
                file_record.remote_url = details_url;
                file_record.last_saf_id = _saf_id;
                file_record.download_status = 2;
                file_record.last_downloaded = DateTime.Now;
                file_record.local_path = full_path;

                // Update file record
                _monitor_repo.StoreStudyFileRec(file_record);
            }
            return addition;
        }


        private string WriteOutFile(string eudract_id, EUCTR_Record st)
        {
            XmlSerializer writer = new XmlSerializer(typeof(EUCTR_Record));
            if (!Directory.Exists(_file_base))
            {
                Directory.CreateDirectory(_file_base);
            }
            string file_name = "EU " + eudract_id + ".xml";
            string full_path = Path.Combine(_file_base, file_name);
            _file_writer.WriteEUCTRFile(writer, st, full_path);
            return full_path;
        }


        private void CheckAccessErrorCount()
        {
            _access_error_num++;
            if (_access_error_num % 5 == 0)
            {
                // do a 5 minute pause
                TimeSpan pause = new TimeSpan(0, 1, 0);
                System.Threading.Thread.Sleep(pause);
                _pause_error_num++;
                _access_error_num = 0;
            }
            if (_pause_error_num > 5)
            {
                //throw new Exception("Too many access errors");
            }
        }

    }
}
