using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;

namespace DataDownloader.ctg
{
    class CTG_Controller
    {
        CTG_Processor _processor;
        Source _source;
        string _file_base;
        FileWriter _file_writer;
        int _saf_id;
        DateTime? _cutoff_date;
        XmlWriterSettings _settings;
        MonitorDataLayer _monitor_repo;
        LoggingHelper _logging_helper;

        public CTG_Controller(int saf_id, Source source, Args args, MonitorDataLayer monitor_repo, LoggingHelper logging_helper)
        {
            _processor = new CTG_Processor();
            _source = source;

            _file_base = source.local_folder;
            _saf_id = saf_id;
            _file_writer = new FileWriter(source);

            _monitor_repo = monitor_repo;
            _logging_helper = logging_helper;
            _cutoff_date = args.cutoff_date;

            //webClient = new HttpClient();
            _settings = new XmlWriterSettings();
            _settings.Async = true;
            _settings.Encoding = System.Text.Encoding.UTF8;
        }


        public async Task<DownloadResult> ProcessDataAsync()
        {
            // Data retrieval is through a file download (for a full dump of the CTG data) or 
            // download via an API call to revised files using a cut off revision date. 
            // The args parameter needs to be inspected to ddetermine which.

            // If a full download the zip file can simply be expanded into the CTG folder area.
            // It is then not necessaery to run this module at all.

            // If an update the new files will be added, the amended files replaced, as necessary.
            // In some cases a search may be carried out to identify the files without downloading them.

            DownloadResult res = new DownloadResult();
            ScrapingHelpers ch = new ScrapingHelpers(_logging_helper);

            if (_cutoff_date != null)
            {
                _cutoff_date = (DateTime)_cutoff_date;
                string year = _cutoff_date.Value.Year.ToString();
                string month = _cutoff_date.Value.Month.ToString("00");
                string day = _cutoff_date.Value.Day.ToString("00");

                int min_rank = 1;
                int max_rank = 20;

                string start_url = "https://clinicaltrials.gov/api/query/full_studies?expr=AREA%5BLastUpdatePostDate%5DRANGE%5B";
                string cut_off_params = month + "%2F" + day + "%2F" + year;
                string end_url = "%2C+MAX%5D&min_rnk=" + min_rank.ToString() + "&max_rnk=" + max_rank.ToString() + "&fmt=xml";
                string url = start_url + cut_off_params + end_url;

                // Do initial search 

                string responseBody = await ch.GetStringFromURLAsync(url);
                if (responseBody != null)
                {
                    XmlDocument xdoc = new XmlDocument();
                    xdoc.LoadXml(responseBody);
                    var num_found_string = xdoc.GetElementsByTagName("NStudiesFound")[0].InnerText;

                    if (Int32.TryParse(num_found_string, out int record_count))
                    {
                        // Then go through the identified records 20 at a time

                        int loop_count = record_count % 20 == 0 ? record_count / 20 : (record_count / 20) + 1;
                        for (int i = 0; i < loop_count; i++)
                        {
                            System.Threading.Thread.Sleep(800);
                            min_rank = (i * 20) + 1;
                            max_rank = (i * 20) + 20;
                            end_url = "%2C+MAX%5D&min_rnk=" + min_rank.ToString() + "&max_rnk=" + max_rank.ToString() + "&fmt=xml";
                            url = start_url + cut_off_params + end_url;

                            responseBody = await ch.GetStringFromURLAsync(url);
                            if (responseBody != null)
                            {
                                xdoc.LoadXml(responseBody);
                                XmlNodeList full_studies = xdoc.GetElementsByTagName("FullStudy");

                                // write each record in turn and update table in mon DB.

                                foreach (XmlNode full_study in full_studies)
                                {
                                    // Obtain basic information from the file - enough for 
                                    // the details to be filed in source_study_data table.

                                    res.num_checked++;
                                    ctg_basics st = _processor.ObtainBasicDetails(full_study, _logging_helper);

                                    // Then write out file.

                                    string folder_path = _file_base + st.file_path;
                                    if (!Directory.Exists(folder_path))
                                    {
                                        Directory.CreateDirectory(folder_path);
                                    }
                                    string full_path = Path.Combine(folder_path, st.file_name);
                                    XmlDocument filedoc = new XmlDocument();
                                    filedoc.LoadXml(full_study.OuterXml);
                                    try
                                    {
                                        filedoc.Save(full_path);
                                    }
                                    catch(Exception e)
                                    {
                                        _logging_helper.LogLine("Error in trying to save file at " + full_path + ":: " + e.Message);
                                    }

                                    // Record details of updated or new record in source_study_data.

                                    bool added = _monitor_repo.UpdateStudyDownloadLog(_source.id, st.sd_sid, st.remote_url, _saf_id,
                                                                      st.last_updated, full_path);
                                    res.num_downloaded++;
                                    if (added) res.num_added++;
                                }

                                if (i % 5 == 0) _logging_helper.LogLine((i * 20).ToString() + " files processed");
                            }

                            // for testing
                            if (i > 5) break;

                        }
                    }
                }
            }

            return res;
        }

    }
}
