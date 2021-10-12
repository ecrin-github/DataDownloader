using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;

namespace DataDownloader.ctg
{
    class CTG_Controller
    {
        CTG_Processor processor;
        Source source;
        string file_base;
        FileWriter file_writer;
        int saf_id;
        LoggingDataLayer logging_repo;
        DateTime? cutoff_date;
        XmlWriterSettings settings;

        public CTG_Controller(int _saf_id, Source _source, Args args, LoggingDataLayer _logging_repo)
        {
            processor = new CTG_Processor();
            source = _source;

            file_base = source.local_folder;
            saf_id = _saf_id;
            file_writer = new FileWriter(source);

            logging_repo = _logging_repo;
            cutoff_date = args.cutoff_date;

            //webClient = new HttpClient();
            settings = new XmlWriterSettings();
            settings.Async = true;
            settings.Encoding = System.Text.Encoding.UTF8;
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
            ScrapingHelpers ch = new ScrapingHelpers(logging_repo);

            if (cutoff_date != null)
            {
                cutoff_date = (DateTime)cutoff_date;
                string year = cutoff_date.Value.Year.ToString();
                string month = cutoff_date.Value.Month.ToString("00");
                string day = cutoff_date.Value.Day.ToString("00");

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
                                    ctg_basics st = processor.ObtainBasicDetails(full_study, logging_repo);

                                    // Then write out file.

                                    string folder_path = file_base + st.file_path;
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
                                        logging_repo.LogLine("Error in trying to save file at " + full_path + ":: " + e.Message);
                                    }

                                    // Record details of updated or new record in source_study_data.

                                    bool added = logging_repo.UpdateStudyDownloadLog(source.id, st.sd_sid, st.remote_url, saf_id,
                                                                      st.last_updated, full_path);
                                    res.num_downloaded++;
                                    if (added) res.num_added++;
                                }

                                if (i % 5 == 0) logging_repo.LogLine((i * 20).ToString() + " files processed");
                            }

                            // for testing
                            // if (i > 5) break;

                        }
                    }
                }
            }

            return res;
        }

    }
}
