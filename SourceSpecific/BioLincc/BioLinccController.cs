using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Html;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace DataDownloader.biolincc
{
    public class BioLINCC_Controller
    {
        ScrapingBrowser _browser;
        BioLinccDataLayer _biolincc_repo;
        BioLINCC_Processor _processor;
        Source _source;
        string _file_base;
        FileWriter _file_writer;
        int _saf_id;
        int _source_id;
        MonitorDataLayer _monitor_repo;
        LoggingHelper _logging_helper;
        int? _days_ago;


        public BioLINCC_Controller(ScrapingBrowser browser, int saf_id, Source source, Args args, MonitorDataLayer monitor_repo, LoggingHelper logging_helper)
        {
            _browser = browser;
            _biolincc_repo = new BioLinccDataLayer();
            _processor = new BioLINCC_Processor();
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
            // For BioLincc, all data is downloaded each time during a download, as it takes a relatively short time
            // and the files simply replaced or - if new - added to the folder. There is therrefore not a concept of an
            // update or focused download, as opposed to a full download.
            
            // Get list of studies from the Biolincc start page.
            ScrapingHelpers ch = new ScrapingHelpers(_browser, _logging_helper);
            WebPage homePage = ch.GetPage("https://biolincc.nhlbi.nih.gov/studies/");
            if (homePage == null)
            {
                _logging_helper.LogError("Initial attempt to access BioLInnc studies list page failed");
                return null;
            }

            var study_list_table = homePage.Find("div", By.Class("table-responsive"));
            HtmlNode[] studyRows = study_list_table.CssSelect("tbody tr").ToArray();
            _logging_helper.LogHeader("Processing Data");
            _logging_helper.LogLine("file list obtained, of " + studyRows.Length + "rows");

            XmlSerializer writer = new XmlSerializer(typeof(BioLincc_Record));
            DownloadResult res = new DownloadResult();

            // Consider each study in turn.

            foreach (HtmlNode row in studyRows)
            {
                res.num_checked++;

                //if (res.num_checked == 3) continue;
                //if (res.num_checked > 5) break;

                BioLincc_Basics bb = _processor.GetStudyBasics(row);
                if (bb.collection_type == "Non-BioLINCC Resource")
                {
                    _logging_helper.LogLine("#" + res.num_checked.ToString() + ": Non-BioLINCC Resource, not processed ");
                }
                else
                {
                    // if record already downloaded today, ignore it... (may happen if re-running after an error)
                    // interrogate study record (if there is one)
                    if (_days_ago == null || !_monitor_repo.Downloaded_recently(_source_id, bb.sd_sid, (int)_days_ago))
                    {
                        // fetch the constructed study record
                        _logging_helper.LogLine("#" + res.num_checked.ToString() + ": " + bb.sd_sid);
                        BioLincc_Record st = _processor.GetStudyDetails(bb, ch, _biolincc_repo, _logging_helper);

                        if (st != null)
                        {
                            // Store the links between Biolincc and NCT records
                            _biolincc_repo.StoreLinks(st.sd_sid, st.registry_ids);

                            // store any nonmatched documents in the table
                            // and abort the download for that record

                            if (st.UnmatchedDocTypes.Count > 0)
                            { 
                                foreach (string s in st.UnmatchedDocTypes)
                                {
                                    _biolincc_repo.InsertUnmatchedDocumentType(s);
                                }
                            }
                            else
                            {
                                // Write out study record as XML.

                                string file_name = _source.local_file_prefix + st.sd_sid + ".xml";
                                string full_path = Path.Combine(_file_base, file_name);
                                _file_writer.WriteBioLINCCFile(writer, st, full_path);
                                bool added = _monitor_repo.UpdateStudyDownloadLog(_source_id, st.sd_sid, st.remote_url, _saf_id,
                                                                  st.last_revised_date, full_path);
                                res.num_downloaded++;
                                if (added) res.num_added++;

                                // Put a pause here 

                                System.Threading.Thread.Sleep(1000);
                            }
                        }
                    }

                    _logging_helper.LogLine("files now downloaded: " + res.num_downloaded.ToString());
                }
            }

            _biolincc_repo.UpdateLinkStatus();
            return res;
        }


        public void PostProcessData()
        {
            // Preliminary processing of data
            // Allows groups of Biolinnc trials that equate to a single NCT registry to be identified
            XmlSerializer writer = new XmlSerializer(typeof(BioLincc_Record));
            IEnumerable<StudyFileRecord> file_list = _monitor_repo.FetchStudyFileRecords(_source.id);
            int n = 0; string filePath = "";
            foreach (StudyFileRecord rec in file_list)
            {
                n++;
                //if (n == 3) continue;
                //if (n > 5) break;

                filePath = rec.local_path;
                if (File.Exists(filePath))
                {
                    string inputString = "";
                    using (var streamReader = new StreamReader(filePath, System.Text.Encoding.UTF8))
                    {
                        inputString += streamReader.ReadToEnd();
                    }

                    XmlSerializer serializer = new XmlSerializer(typeof(BioLincc_Record));
                    StringReader rdr = new StringReader(inputString);
                    BioLincc_Record studyRegEntry = (BioLincc_Record)serializer.Deserialize(rdr);

                    // update the linkage data 
                    studyRegEntry.in_multiple_biolincc_group = _biolincc_repo.GetMultiLinkStatus(rec.sd_id);

                    // and reserialise
                    string file_name = _source.local_file_prefix + rec.sd_id + ".xml";
                    string full_path = Path.Combine(_file_base, file_name);
                    _file_writer.WriteBioLINCCFile(writer, studyRegEntry, full_path);

                }

                if (n % 10 == 0) _logging_helper.LogLine("Updated " + n.ToString());
            }
        }
    }
}
