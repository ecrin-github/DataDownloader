using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Network;
using ScrapySharp.Html;
using System.IO;
using System.Web;
using System.Xml.Serialization;

namespace DataDownloader.euctr
{
    public class EUCTR_Processor
    {
       public void GetStudyInitialDetails(ScrapingBrowser browser, WebPage homePage, LoggingDataLayer logging_repo, 
                                           int pagenum, string file_base, int source_id, int last_saf_id, bool incomplete_only)
        {
            int n = (pagenum - 1) * 20;
            var pageContent = homePage.Find("div", By.Class("results"));
            HtmlNode[] studyBoxes = pageContent.CssSelect(".result").ToArray();
            XmlSerializer writer = new XmlSerializer(typeof(EUCTR_Record));

            foreach (HtmlNode box in studyBoxes)
            {
                n++;

                // Ids and start date - three td elements in a fixed order in first row.
                HtmlNode[] studyDetails = box.Elements("tr").ToArray();
                HtmlNode[] idDetails = studyDetails[0].CssSelect("td").ToArray();
                string euctr_id = InnerValue(idDetails[0]);

                // Now the euctr_id (sd_id) is known get file record - check OK to proceed...

                StudyFileRecord file_record = logging_repo.FetchStudyFileRecord(euctr_id, source_id);
                if (file_record?.last_saf_id == last_saf_id) continue;  // already been done - used when skipping after errors...

                // If 'incomplete_only' has been signalled ignore 'assumed complete' files.

                if (incomplete_only && file_record.assume_complete == true) continue; 
                
                // Instantiate record.

                string sponsor_id = InnerValue(idDetails[1]);
                string start_date = InnerValue(idDetails[2]);
                EUCTR_Record st = new EUCTR_Record(n, euctr_id, sponsor_id, start_date);

                // Get other details in the box on the search page.

                st = ExtractSingleRecord(st, box, studyDetails, browser);

                // Get details from the protocol details page.

                WebPage detailsPage = null;
                detailsPage = browser.NavigateToPage(new Uri(st.details_url));
                if (detailsPage != null)
                {
                    st = ExtractProtocolDetails(st, detailsPage);
                }

                // Get results details

                if (st.results_url != null)
                {
                    System.Threading.Thread.Sleep(1000);
                    WebPage resultsPage = browser.NavigateToPage(new Uri(st.results_url));

                    if (resultsPage != null)
                    {
                        st = ExtractResultDetails(st, resultsPage);
                    }
                }

                // Write out study record as XML.

                string file_name = "EU " + st.eudract_id + ".xml";
                string full_path = Path.Combine(file_base, file_name);
                CreateFile(writer, st, full_path);

                // Update file record in the mon database.

                if (file_record == null)
                {
                    // new record
                    bool? assume_complete = (st.trial_status == "Completed" && st.results_url != null) ? (bool?)true : null;
                    file_record = new StudyFileRecord(source_id, st.eudract_id, st.details_url, last_saf_id, assume_complete, full_path);
                    logging_repo.InsertStudyFileRec(file_record);
                }
                else
                {
                    // update record
                    file_record.remote_url = st.details_url;
                    file_record.last_saf_id = last_saf_id;
                    file_record.assume_complete = (st.trial_status == "Completed" && st.results_url != null) ? (bool?)true : null;
                    file_record.download_status = 2;
                    file_record.last_downloaded = DateTime.Now;
                    file_record.local_path = full_path;
                    logging_repo.StoreStudyFileRec(file_record);
                }

                System.Threading.Thread.Sleep(1500);
            }
        }



        public void GetSingleStudyDetails(ScrapingBrowser browser, WebPage homePage, LoggingDataLayer logging_repo,
                                          string file_base, int source_id, int last_saf_id)
        {
            // This used when examining only a single study

            var pageContent = homePage.Find("div", By.Class("results"));
            HtmlNode[] studyBoxes = pageContent.CssSelect(".result").ToArray();
            XmlSerializer writer = new XmlSerializer(typeof(EUCTR_Record));

            foreach (HtmlNode box in studyBoxes)
            {
                int n = -1;
                // Ids and start date - three td elements in a fixed order in first row.
                HtmlNode[] studyDetails = box.Elements("tr").ToArray();
                HtmlNode[] idDetails = studyDetails[0].CssSelect("td").ToArray();
                string euctr_id = InnerValue(idDetails[0]);

                // Now the euctr_id (sd_id) is known get file record - check OK to proceed...

                StudyFileRecord file_record = logging_repo.FetchStudyFileRecord(euctr_id, source_id);
                if (file_record?.last_saf_id == last_saf_id) continue;  // already been done

                // Instantiate record.

                string sponsor_id = InnerValue(idDetails[1]);
                string start_date = InnerValue(idDetails[2]);
                EUCTR_Record st = new EUCTR_Record(n, euctr_id, sponsor_id, start_date);

                // Get other details in the box on the search page.

                st = ExtractSingleRecord(st, box, studyDetails, browser);

                // Get details from the protocol details page.

                WebPage detailsPage = null;
                detailsPage = browser.NavigateToPage(new Uri(st.details_url));
                if (detailsPage != null)
                {
                    st = ExtractProtocolDetails(st, detailsPage);
                }

                // Get results details
                if (st.results_url != null)
                {
                    System.Threading.Thread.Sleep(1000);
                    WebPage resultsPage = browser.NavigateToPage(new Uri(st.results_url));

                    if (resultsPage != null)
                    {
                        st = ExtractResultDetails(st, resultsPage);
                    }
                }

                // Write out study record as XML.

                string file_name = "EU " + st.eudract_id + ".xml";
                string full_path = Path.Combine(file_base, file_name);
                CreateFile(writer, st, full_path);

                // Update file record in the mon database.

                if (file_record == null)
                {
                    // new record
                    bool? assume_complete = (st.trial_status == "Completed" && st.results_url != null) ? (bool?)true : null;
                    file_record = new StudyFileRecord(source_id, st.eudract_id, st.details_url, last_saf_id, assume_complete, full_path);
                    logging_repo.InsertStudyFileRec(file_record);
                }
                else
                {
                    // update record
                    file_record.remote_url = st.details_url;
                    file_record.last_saf_id = last_saf_id;
                    file_record.assume_complete = (st.trial_status == "Completed" && st.results_url != null) ? (bool?)true : null;
                    file_record.download_status = 2;
                    file_record.last_downloaded = DateTime.Now;
                    file_record.local_path = full_path;
                    logging_repo.StoreStudyFileRec(file_record);
                }

                System.Threading.Thread.Sleep(1500);
            }
        }
        
        
        private EUCTR_Record ExtractSingleRecord(EUCTR_Record st, HtmlNode box, HtmlNode[] studyDetails, ScrapingBrowser browser)
        {

            // sponsor name in second row - also extracted later from the details page
            st.sponsor_name = InnerValue(studyDetails[1]);
            if (st.sponsor_name.Contains("[...]"))
            {
                st.sponsor_name = st.sponsor_name.Replace("[...]", "");
            }

            // title in third row - get later from the details page

            // medical conditiona as a text description
            st.medical_condition = InnerValue(studyDetails[3]);

            // Disease (MedDRA details) - five td elements in a fixed order, 
            // if they are there at all appear in a nested table, class = 'meddra'.

            List<MeddraTerm> meddra_terms = new List<MeddraTerm>();
            HtmlNode meddraTable = studyDetails[4].CssSelect(".meddra").FirstOrDefault();
            if (meddraTable != null)
            {
                // this table has at 2 least rows, the first with the headers (so row 0 can be ignored)
                HtmlNode[] disDetails = meddraTable.Descendants("tr").ToArray();
                for (int k = 1; k < disDetails.Count(); k++)
                {
                    MeddraTerm stm = new MeddraTerm();
                    HtmlNode[] meddraDetails = disDetails[k].Elements("td").ToArray();
                    stm.version = meddraDetails[0].InnerText?.Trim() ?? "";
                    stm.soc_term = meddraDetails[1].InnerText?.Trim() ?? "";
                    stm.code = meddraDetails[2].InnerText?.Trim() ?? "";
                    stm.term = meddraDetails[3].InnerText?.Trim() ?? "";
                    stm.level = meddraDetails[4].InnerText?.Trim() ?? "";
                    meddra_terms.Add(stm);
                }

                st.meddra_terms = meddra_terms;
            }

            // population age and gender - 2 td elements in a fixed order
            HtmlNode[] popDetails = studyDetails[5].CssSelect("td").ToArray();
            st.population_age = InnerValue(popDetails[0]);
            st.gender = InnerValue(popDetails[1]);

            // Protocol links 
            // These are often multiple but - for the time being at least
            // we take the first and use that to obtain further details
            HtmlNode link = studyDetails[6].CssSelect("a").FirstOrDefault();
            HtmlNode status_node = studyDetails[6].CssSelect("span.status").FirstOrDefault();

            st.details_url = "https://www.clinicaltrialsregister.eu" + link.Attributes["href"].Value;
            if (status_node != null)
            {
                string status = status_node.InnerText ?? "";
                // remove surrounding brackets
                if (status != "" && status.StartsWith("(") && status.Length > 2)
                {
                    status = status.Substring(1, status.Length - 2);
                }
                st.trial_status = status;
            }

            // Results link, if any
            HtmlNode resultLink = studyDetails[7].CssSelect("a").FirstOrDefault();
            if (resultLink != null)
            {
                st.results_url = "https://www.clinicaltrialsregister.eu" + resultLink.Attributes["href"].Value;

                // if results link present and Status not completed make status "Completed"
                // (some entries may not have been updated)
                if (st.trial_status != "Completed") st.trial_status = "Completed";
            }

            return st;
        }


        private EUCTR_Record ExtractProtocolDetails(EUCTR_Record st, WebPage detailsPage)
        {
            var summary = detailsPage.Find("table", By.Class("summary")).FirstOrDefault();

            // if no summary probably missing page - seems to occur for one record
            if (summary == null) return st;

            // get the date added to system from the 6th row of the summary table
            var summary_rows = summary.CssSelect("tbody tr").ToArray();
            if (summary_rows != null)
            {
                foreach (HtmlNode row in summary_rows)
                {
                    var cells = row.CssSelect("td").ToArray();
                    if (cells[0].InnerText.Substring(0, 8) == "Date on ")
                    {
                        st.entered_in_db = cells[1].InnerText;
                        break;
                    }
                }
            }

            var identifiers = detailsPage.Find("table", By.Id("section-a")).FirstOrDefault();
            var identifier_rows = identifiers.CssSelect("tbody tr").ToArray();
            if (identifier_rows != null)
            {
                st.identifiers = GetStudyIdentifiers(identifier_rows);
            }

            var sponsor = detailsPage.Find("table", By.Id("section-b")).FirstOrDefault();
            var sponsor_rows = sponsor.CssSelect("tbody tr").ToArray();
            if (sponsor_rows != null)
            {
                st.sponsors = GetStudySponsors(sponsor_rows);
            }

            var imp_details = detailsPage.Find("table", By.Id("section-d")).FirstOrDefault();
            var imp_tables = imp_details.CssSelect("tbody").ToArray();
            if (imp_tables != null)
            {
                st.imps = GetStudyIMPs(imp_tables);
            }

            var study_details = detailsPage.Find("table", By.Id("section-e")).FirstOrDefault();
            var details_rows = study_details.CssSelect("tbody tr").ToArray();
            if (details_rows != null)
            {
                st.features = GetStudyFeatures(identifier_rows);
            }

            var population = detailsPage.Find("table", By.Id("section-f")).FirstOrDefault();
            var population_rows = population.CssSelect("tbody tr").ToArray();
            if (population_rows != null)
            {
                st.population = GetStudyPopulation(identifier_rows);
            }

            return st;
        }
        

        private List<DetailLine> GetStudyIdentifiers(HtmlNode[] identifier_rows)
        {
            List<DetailLine> study_identifiers = new List<DetailLine>();

            foreach (HtmlNode row in identifier_rows)
            {
                var row_class = row.Attributes["class"];
                if (row_class != null && row_class.Value == "tricell")
                {
                    var cells = row.CssSelect("td").ToArray();
                    string code = cells[0].InnerText;
                    if (code != "A.6" && code != "A.7" && code != "A.8")
                    {
                        DetailLine line = new DetailLine();
                        List<item_value> values = new List<item_value>();
                        line.item_code = code;
                        line.item_name = HttpUtility.HtmlDecode(cells[1].InnerText);
                        if (cells[2].CssSelect("table").Any())
                        {
                            var inner_table = cells[2].CssSelect("table");
                            var inner_rows = inner_table.CssSelect("tr").ToArray();
                            if (inner_rows.Count() > 0)
                            {
                                foreach (HtmlNode inner_row in inner_rows)
                                {
                                    var inner_cell = inner_row.CssSelect("td").FirstOrDefault();
                                    string value = HttpUtility.HtmlDecode(inner_cell.InnerText).Trim();
                                    if (!string.IsNullOrEmpty(value)) values.Add(new item_value(value));
                                }
                            }
                        }
                        else
                        {
                            line.item_number = 1;
                            string value = HttpUtility.HtmlDecode(cells[2].InnerText).Trim();
                            if (!string.IsNullOrEmpty(value)) values.Add(new item_value(value));
                        }

                        if (values.Count > 0)
                        {
                            line.item_number = values.Count;
                            line.item_values = values;
                            study_identifiers.Add(line);
                        }
                    }
                }
            }

            return study_identifiers;
        }


        private List<DetailLine> GetStudySponsors(HtmlNode[] sponsor_rows)
        {
            List<DetailLine> study_sponsors = new List<DetailLine>();
            
            foreach (HtmlNode row in sponsor_rows)
            {
                var row_class = row.Attributes["class"];
                if (row_class != null && row_class.Value == "tricell")
                {
                    var cells = row.CssSelect("td").ToArray();
                    string code = cells[0].InnerText;
                    if (!code.Contains("B.5"))
                    {
                        DetailLine line = new DetailLine();
                        List<item_value> values = new List<item_value>();
                        line.item_code = code;
                        line.item_name = HttpUtility.HtmlDecode(cells[1].InnerText);
                        if (cells[2].CssSelect("table").Any())
                        {
                            var inner_table = cells[2].CssSelect("table");
                            var inner_rows = inner_table.CssSelect("tr").ToArray();
                            line.item_number = inner_rows.Length;
                            if (inner_rows.Count() > 0)
                            {
                                foreach (HtmlNode inner_row in inner_rows)
                                {
                                    var inner_cell = inner_row.CssSelect("td").FirstOrDefault();
                                    string value = HttpUtility.HtmlDecode(inner_cell.InnerText).Trim();
                                    if (!string.IsNullOrEmpty(value)) values.Add(new item_value(value));
                                }
                            }
                        }
                        else
                        {
                            line.item_number = 1;
                            string value = HttpUtility.HtmlDecode(cells[2].InnerText).Trim();
                            if (!string.IsNullOrEmpty(value)) values.Add(new item_value(value));
                        }

                        if (values.Count > 0)
                        {
                            line.item_number = values.Count;
                            line.item_values = values;
                            study_sponsors.Add(line);
                        }
                    }
                }
            }

            return study_sponsors;
        }


        private List<ImpLine> GetStudyIMPs(HtmlNode[] imp_tables)
        {
            List<ImpLine> study_imps = new List<ImpLine>();

            int imp_num = 0;
            foreach (HtmlNode tbody in imp_tables)
            {
                imp_num++;
                var imp_rows = tbody.CssSelect("tr").ToArray();
                if (imp_rows != null)
                {
                    foreach (HtmlNode row in imp_rows)
                    {
                        var row_class = row.Attributes["class"];
                        if (row_class != null && row_class.Value == "tricell")
                        {
                            var cells = row.CssSelect("td").ToArray();
                            string code = cells[0].InnerText;
                            // just get various names (ofgen duplicated)
                            if (code == "D.2.1.1.1" || code == "D.3.1" || code == "D.3.8"
                                || code == "D.3.9.1" || code == "D.3.9.3")
                            {
                                ImpLine line = new ImpLine();
                                line.imp_number = imp_num;
                                List<item_value> values = new List<item_value>();
                                line.item_code = code;
                                line.item_name = HttpUtility.HtmlDecode(cells[1].InnerText);
                                if (cells[2].CssSelect("table").Any())
                                {
                                    var inner_table = cells[2].CssSelect("table");
                                    var inner_rows = inner_table.CssSelect("tr").ToArray();
                                    line.item_number = inner_rows.Length;
                                    if (inner_rows.Count() > 0)
                                    {
                                        foreach (HtmlNode inner_row in inner_rows)
                                        {
                                            var inner_cell = inner_row.CssSelect("td").FirstOrDefault();
                                            string value = HttpUtility.HtmlDecode(inner_cell.InnerText).Trim();
                                            if (!string.IsNullOrEmpty(value)) values.Add(new item_value(value));
                                        }
                                    }
                                }
                                else
                                {
                                    line.item_number = 1;
                                    string value = HttpUtility.HtmlDecode(cells[2].InnerText).Trim();
                                    if (!string.IsNullOrEmpty(value)) values.Add(new item_value(value));
                                }

                                if (values.Count > 0)
                                {
                                    line.item_number = values.Count;
                                    line.item_values = values;
                                    study_imps.Add(line);
                                }
                            }
                        }
                    }
                }
            }

            return study_imps;
        }


        private List<DetailLine> GetStudyFeatures(HtmlNode[] details_rows)
        {
            List<DetailLine> study_features = new List<DetailLine>();
            
            foreach (HtmlNode row in details_rows)
            {
                var row_class = row.Attributes["class"];
                if (row_class != null && row_class.Value == "tricell")
                {
                    var cells = row.CssSelect("td").ToArray();
                    string code = cells[0].InnerText;
                    if (code.Contains("E.1.1") || code.Contains("E.2"))
                    {
                        DetailLine line = new DetailLine();
                        List<item_value> values = new List<item_value>();
                        line.item_code = code;
                        line.item_name = HttpUtility.HtmlDecode(cells[1].InnerText);
                        if (cells[2].CssSelect("table").Any())
                        {
                            var inner_table = cells[2].CssSelect("table");
                            var inner_rows = inner_table.CssSelect("tr").ToArray();
                            line.item_number = inner_rows.Length;
                            if (inner_rows.Count() > 0)
                            {
                                foreach (HtmlNode inner_row in inner_rows)
                                {
                                    var inner_cell = inner_row.CssSelect("td").FirstOrDefault();
                                    string value = HttpUtility.HtmlDecode(inner_cell.InnerText).Trim();
                                    if (!string.IsNullOrEmpty(value)) values.Add(new item_value(value));
                                }
                            }
                        }
                        else
                        {
                            line.item_number = 1;
                            string value = HttpUtility.HtmlDecode(cells[2].InnerText).Trim();
                            if (!string.IsNullOrEmpty(value)) values.Add(new item_value(value.Replace("|", "<br/>")));
                        }

                        if (values.Count > 0)
                        {
                            line.item_number = values.Count;
                            line.item_values = values;
                            study_features.Add(line);
                        }
                    }


                    if (code.Contains("E.6") || code.Contains("E.7")
                        || code.Contains("E.8.1") || code.Contains("E.8.2"))
                    {
                        DetailLine line = new DetailLine();
                        List<item_value> values = new List<item_value>();
                        line.item_code = code;
                        line.item_name = HttpUtility.HtmlDecode(cells[1].InnerText);
                        if (cells[2].CssSelect("table").Any())
                        {
                            var inner_table = cells[2].CssSelect("table");
                            var inner_rows = inner_table.CssSelect("tr").ToArray();
                            line.item_number = inner_rows.Length;
                            if (inner_rows.Count() > 0)
                            {
                                foreach (HtmlNode inner_row in inner_rows)
                                {
                                    var inner_cell = inner_row.CssSelect("td").FirstOrDefault();
                                    string value = HttpUtility.HtmlDecode(inner_cell.InnerText).Trim();
                                    if (value.ToLower() == "yes") values.Add(new item_value(value));
                                }
                            }
                        }
                        else
                        {
                            line.item_number = 1;
                            string value = HttpUtility.HtmlDecode(cells[2].InnerText).Trim();
                            if (value.ToLower() == "yes") values.Add(new item_value(value));
                        }

                        if (values.Count > 0)
                        {
                            line.item_number = values.Count;
                            line.item_values = values;
                            study_features.Add(line);
                        }
                    }
                }
            }

            return study_features;
        }


        private List<DetailLine> GetStudyPopulation(HtmlNode[] population_rows)
        {
            List<DetailLine> study_population = new List<DetailLine>();
            
            foreach (HtmlNode row in population_rows)
            {
                var row_class = row.Attributes["class"];
                if (row_class != null && row_class.Value == "tricell")
                {
                    var cells = row.CssSelect("td").ToArray();
                    string code = cells[0].InnerText;
                    if (code.Contains("F.1") || code.Contains("F.2"))
                    {
                        DetailLine line = new DetailLine();
                        List<item_value> values = new List<item_value>();
                        line.item_code = code;
                        line.item_name = HttpUtility.HtmlDecode(cells[1].InnerText);
                        if (cells[2].CssSelect("table").Any())
                        {
                            var inner_table = cells[2].CssSelect("table");
                            var inner_rows = inner_table.CssSelect("tr").ToArray();
                            line.item_number = inner_rows.Length;
                            if (inner_rows.Count() > 0)
                            {
                                foreach (HtmlNode inner_row in inner_rows)
                                {
                                    var inner_cell = inner_row.CssSelect("td").FirstOrDefault();
                                    string value = HttpUtility.HtmlDecode(inner_cell.InnerText).Trim();
                                    if (value.ToLower() == "yes") values.Add(new item_value(value));
                                }
                            }
                        }
                        else
                        {
                            line.item_number = 1;
                            string value = HttpUtility.HtmlDecode(cells[2].InnerText).Trim();
                            if (value.ToLower() == "yes") values.Add(new item_value(value));
                        }

                        if (values.Count > 0)
                        {
                            line.item_number = values.Count;
                            line.item_values = values;
                            study_population.Add(line);
                        }
                    }
                }
            }

            return study_population;
        }


        private EUCTR_Record ExtractResultDetails(EUCTR_Record st, WebPage resultsPage)
        {

            var pdfLInk = resultsPage.Find("a", By.Id("downloadResultPdf")).FirstOrDefault();

            if (pdfLInk != null)
            {
                st.results_pdf_link = pdfLInk.Attributes["href"].Value;
            }

            var result_div = resultsPage.Find("div", By.Id("resultContent")).FirstOrDefault();

            if (result_div != null)
            {
                var result_rows = result_div.SelectNodes("table[1]/tr")?.ToArray();
                if (result_rows != null)
                {
                    for (int i = 0; i<result_rows.Length;  i++)
                        {
                        var first_cell = result_rows[i].SelectSingleNode("td[1]");
                        string first_cell_content = TrimmedContents(first_cell);

                        if (first_cell_content == "Results version number")
                        {
                            st.results_version = TrimmedContents(first_cell.SelectSingleNode("following-sibling::td[1]"));
                        }
                        else    if (first_cell_content == "This version publication date")
                        {
                            st.results_revision_date = TrimmedContents(first_cell.SelectSingleNode("following-sibling::td[1]"));
                        }
                        else  if (first_cell_content == "First version publication date")
                        {
                            st.results_first_date = TrimmedContents(first_cell.SelectSingleNode("following-sibling::td[1]"));
                        }
                        else  if (first_cell_content == "Summary report(s)")
                        {
                            st.results_summary_link = first_cell.SelectSingleNode("following-sibling::td[1]/a[1]").Attributes["href"].Value;
                            st.results_summary_name = TrimmedContents(first_cell.SelectSingleNode("following-sibling::td[1]/a[1]"));
                        }
                    }
                }

            }

            return st;
        }


        private void CreateFile(XmlSerializer writer, EUCTR_Record st, string full_path)
        {
            if (File.Exists(full_path))
            {
                File.Delete(full_path);
            }
            FileStream file = System.IO.File.Create(full_path);
            writer.Serialize(file, st);
            file.Close();
        }


        private string InnerValue(HtmlNode node)
        {
            string allInner = node.InnerText?.Replace("\n", "")?.Replace("\r", "")?.Trim() ?? "";
            string label = node.CssSelect(".label").FirstOrDefault().InnerText?.Trim() ?? "";
            return allInner.Substring(label.Length)?.Trim() ?? "";
        }


        private string TrimmedContents(HtmlNode node)
         {
            return node.InnerText?.Replace("\n", "")?.Replace("\r", "")?.Trim() ?? "";
        }


        private string TrimmedLabel(HtmlNode node)
        {
            return node.CssSelect(".label").FirstOrDefault().InnerText?.Trim() ?? "";
        }

        
    }
}
