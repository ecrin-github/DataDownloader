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
        ScrapingBrowser browser;
        BioLinccDataLayer biolincc_repo;
        BioLINCC_Processor processor;
        Source source;
        string file_base;
        FileWriter file_writer;
        int saf_id;
        int source_id;
        LoggingDataLayer logging_repo;
        int? days_ago;


        public BioLINCC_Controller(ScrapingBrowser _browser, int _saf_id, Source _source, Args args, LoggingDataLayer _logging_repo)
        {
            browser = _browser;
            biolincc_repo = new BioLinccDataLayer();
            processor = new BioLINCC_Processor();
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
            // For BioLincc, all data is downloaded each time during a download, as it takes a relatively short time
            // and the files simply replaced or - if new - added to the folder. There is therrefore not a concept of an
            // update or focused download, as opposed to a full download.
            
            // Get list of studies from the Biolincc start page.
            ScrapingHelpers ch = new ScrapingHelpers(browser, logging_repo);
            WebPage homePage = ch.GetPage("https://biolincc.nhlbi.nih.gov/studies/");
            if (homePage == null)
            {
                logging_repo.LogError("Initial attempt to access BioLInnc studies list page failed");
                return null;
            }

            var study_list_table = homePage.Find("div", By.Class("table-responsive"));
            HtmlNode[] studyRows = study_list_table.CssSelect("tbody tr").ToArray();
            logging_repo.LogHeader("Processing Data");
            logging_repo.LogLine("file list obtained, of " + studyRows.Length + "rows");

            XmlSerializer writer = new XmlSerializer(typeof(BioLincc_Record));
            DownloadResult res = new DownloadResult();

            // recreate this table each time
            biolincc_repo.RecreateMultiHBLIsTable();

            // Consider each study in turn.

            foreach (HtmlNode row in studyRows)
            {
                res.num_checked++;

                //if (res.num_checked == 3) continue;
                //if (res.num_checked > 5) break;

                BioLincc_Basics bb = processor.GetStudyBasics(row);
                if (bb.collection_type == "Non-BioLINCC Resource")
                {
                    logging_repo.LogLine("#" + res.num_checked.ToString() + ": Non-BioLINCC Resource, not processed ");
                }
                else
                {
                    // if record already downloaded today, ignore it... (may happen if re-running after an error)
                    // interrogate study record (if there is one)
                    if (days_ago == null || !logging_repo.Downloaded_recently(source_id, bb.sd_sid, (int)days_ago))
                    {
                        // fetch the constructed study record
                        logging_repo.LogLine("#" + res.num_checked.ToString() + ": " + bb.sd_sid);
                        BioLincc_Record st = processor.GetStudyDetails(bb, ch, biolincc_repo, logging_repo);

                        if (st != null)
                        {
                            // Store the links between Biolincc and NCT records
                            biolincc_repo.StoreLinks(st.sd_sid, st.registry_ids);

                            // store any nonmatched documents in the table
                            // and abort the download for that record

                            if (st.UnmatchedDocTypes.Count > 0)
                            {
                                foreach (string s in st.UnmatchedDocTypes)
                                {
                                    biolincc_repo.InsertUnmatchedDocumentType(s);
                                }
                            }
                            else
                            {
                                // Write out study record as XML.

                                string file_name = source.local_file_prefix + st.sd_sid + ".xml";
                                string full_path = Path.Combine(file_base, file_name);
                                file_writer.WriteBioLINCCFile(writer, st, full_path);
                                bool added = logging_repo.UpdateStudyDownloadLog(source_id, st.sd_sid, st.remote_url, saf_id,
                                                                  st.last_revised_date, full_path);
                                res.num_downloaded++;
                                if (added) res.num_added++;

                                // Put a pause here 

                                System.Threading.Thread.Sleep(1000);
                            }
                        }
                    }

                    logging_repo.LogLine("files now downloaded: " + res.num_downloaded.ToString());
                }
            }

            biolincc_repo.UpdateLinkStatus();
            return res;
        }


        public void PostProcessData()
        {
            // Preliminary processing of data
            // Allows groups of Biolinnc trials that equate to a single NCT registry to be identified
            XmlSerializer writer = new XmlSerializer(typeof(BioLincc_Record));
            IEnumerable<StudyFileRecord> file_list = logging_repo.FetchStudyFileRecords(source.id);
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
                    studyRegEntry.in_multiple_biolincc_group = biolincc_repo.GetMultiLinkStatus(rec.sd_id);

                    // and reserialise
                    string file_name = source.local_file_prefix + rec.sd_id + ".xml";
                    string full_path = Path.Combine(file_base, file_name);
                    file_writer.WriteBioLINCCFile(writer, studyRegEntry, full_path);

                }

                if (n % 10 == 0) logging_repo.LogLine("Updated " + n.ToString());
            }
        }
    }
}
