using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Html;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DataDownloader.euctr
{
    public class EUCTR_Processor
    {
        public int GetListLength(WebPage homePage)
        {
            // gets the numbers of records found for the current search
            var total_link = homePage.Find("a", By.Id("ui-id-1")).FirstOrDefault();
            string results = TrimmedContents(total_link);
            int left_bracket_pos = results.IndexOf("(");
            int right_bracket_pos = results.IndexOf(")");
            string results_value = results.Substring(left_bracket_pos + 1, right_bracket_pos - left_bracket_pos - 1);
            results_value = results_value.Replace(",", "");
            if (Int32.TryParse(results_value, out int result_count))
            {
                return result_count;
            }
            else
            {
                return 0;
            }
        }

 
        public List<EUCTR_Summmary> GetStudyList(WebPage homePage, bool doAll = false)
        {
            // doAll parameter, by default false sets the do_download
            // property of the summary record. If true all records will be downloaded.

            List<EUCTR_Summmary> summaries = new List<EUCTR_Summmary>();

            var pageContent = homePage.Find("div", By.Class("results"));
            HtmlNode[] studyBoxes = pageContent.CssSelect(".result").ToArray();
            foreach (HtmlNode box in studyBoxes)
            {
                // Ids and start date - three td elements in a fixed order in first row.
                HtmlNode[] studyDetails = box.Elements("tr").ToArray();
                HtmlNode[] idDetails = studyDetails[0].CssSelect("td").ToArray();

                string euctr_id = InnerValue(idDetails[0]);
                string sponsor_id = InnerValue(idDetails[1]);
                string start_date = InnerValue(idDetails[2]);

                summaries.Add(new EUCTR_Summmary(euctr_id, sponsor_id, start_date, doAll));
            }
            return summaries;
        }


        public EUCTR_Summmary DeriveFullSummary(WebPage homePage, EUCTR_Summmary s, int i)
        {
            // receives reference to the search page and the summary list
            // Completes summary details if the etudy is to be downloaded

            var pageContent = homePage.Find("div", By.Class("results"));
            HtmlNode[] studyBoxes = pageContent.CssSelect(".result").ToArray();

            HtmlNode box = studyBoxes[i];
            
            //check that these match...
            // Ids and start date - three td elements in a fixed order in first row.
            HtmlNode[] studyDetails = box.Elements("tr").ToArray();
            HtmlNode[] idDetails = studyDetails[0].CssSelect("td").ToArray();

            string euctr_id = InnerValue(idDetails[0]);
            
            if (euctr_id != s.eudract_id)
            {
                // we have a problem!
                string message = "At position " + i.ToString() + " on " + homePage.BaseUrl.ToString() +
                                    " had a EUCTR id of " + s.eudract_id +
                                    " in the summary list and an id of " + euctr_id +
                                    " on the page ";
                s.trial_status = message;
            }
            else
            {
                // Get other details in the box on the search page.

                // sponsor name in second row - also extracted later from the details page
                s.sponsor_name = InnerValue(studyDetails[1]);
                if (s.sponsor_name.Contains("[...]"))
                {
                    s.sponsor_name = s.sponsor_name.Replace("[...]", "");
                }

                // medical conditiona as a text description
                s.medical_condition = InnerValue(studyDetails[3]);

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

                    s.meddra_terms = meddra_terms;
                }

                // population age and gender - 2 td elements in a fixed order
                HtmlNode[] popDetails = studyDetails[5].CssSelect("td").ToArray();
                s.population_age = InnerValue(popDetails[0]);
                s.gender = InnerValue(popDetails[1]);

                // Protocol links 
                // These are often multiple and we need to consider the whole
                // list to get the countries involved.

                List<Country> countries = new List<Country>();
                List<HtmlNode> links = studyDetails[6].CssSelect("a").ToList();
                List<HtmlNode> statuses = studyDetails[6].CssSelect("span").ToList();
                char[] parantheses = { '(', ')' };

                // Because of an additional initial 'Trial protocol:' span
                // there should be links + 1 span (status) numbers
                // first valid status found used as overall trial status

                if (links.Count == statuses.Count - 1)
                {
                    // get country names and study status
                    // For GB status no longer available for ongoing studies

                    for (int j = 0; j < links.Count; j++)
                    {
                        int status_num = 0;
                        string country_code = links[j].InnerText;
                        string country_name = GetCountryName(country_code);
                        if (country_name != "")
                        {
                            string study_status = statuses[j + 1].InnerText.Trim(parantheses);
                            if (study_status != "GB - no longer in EU/EEA")
                            {
                                countries.Add(new Country(country_name, study_status));
                                status_num++;
                                if (status_num == 1)
                                {
                                    s.trial_status = study_status;
                                }
                            }
                            else
                            {
                                countries.Add(new Country(country_name, null));
                            }
                        }
                    }
                }
                else
                {
                    // just get the country names

                    for (int j = 0; j < links.Count; j++)
                    {
                        string country_code = links[j].InnerText;
                        string country_name = GetCountryName(country_code);
                        if (country_name != "")
                        {
                            countries.Add(new Country(country_name, ""));
                        }
                    }
                }

                s.countries = countries;

                // Only the first listed country used to obtain the protocol details and overall trial status

                s.details_url = "https://www.clinicaltrialsregister.eu" + links[0].Attributes["href"].Value;

                // Results link, if any
                HtmlNode resultLink = studyDetails[7].CssSelect("a").FirstOrDefault();

                if (resultLink != null)
                {
                    s.results_url = "https://www.clinicaltrialsregister.eu" + resultLink.Attributes["href"].Value;

                    // if results link present and Status not completed make status "Completed"
                    // (some entries may not have been updated)

                    if (s.trial_status != "Completed") s.trial_status = "Completed";
                }
            }

            return s;       
        }

  
        public EUCTR_Record ExtractProtocolDetails(EUCTR_Record st, WebPage detailsPage)
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

                    if (cells[0].InnerText.StartsWith("Date on "))
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
                st.features = GetStudyFeatures(details_rows, st.countries);
            }

            var population = detailsPage.Find("table", By.Id("section-f")).FirstOrDefault();
            var population_rows = population.CssSelect("tbody tr").ToArray();
            if (population_rows != null)
            {
                st.population = GetStudyPopulation(population_rows);
            }

            return st;
        }


        public EUCTR_Record ExtractResultDetails(EUCTR_Record st, WebPage resultsPage)
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
                    for (int i = 0; i < result_rows.Length; i++)
                    {
                        var first_cell = result_rows[i].SelectSingleNode("td[1]");
                        string first_cell_content = TrimmedContents(first_cell);

                        if (first_cell_content == "Results version number")
                        {
                            st.results_version = TrimmedContents(first_cell.SelectSingleNode("following-sibling::td[1]"));
                        }
                        else if (first_cell_content == "This version publication date")
                        {
                            st.results_revision_date = TrimmedContents(first_cell.SelectSingleNode("following-sibling::td[1]"));
                        }
                        else if (first_cell_content == "First version publication date")
                        {
                            st.results_first_date = TrimmedContents(first_cell.SelectSingleNode("following-sibling::td[1]"));
                        }
                        else if (first_cell_content == "Summary report(s)")
                        {
                            st.results_summary_link = first_cell.SelectSingleNode("following-sibling::td[1]/a[1]").Attributes["href"].Value;
                            st.results_summary_name = TrimmedContents(first_cell.SelectSingleNode("following-sibling::td[1]/a[1]"));
                        }
                    }
                }

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


        private List<DetailLine> GetStudyFeatures(HtmlNode[] details_rows, List<Country> summ_countries)
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


                    if (code == "E.8.6.3")
                    {
                        //may have a list of one or more countries in an internal table

                        var inner_table = cells[2].CssSelect("table");
                        var inner_rows = inner_table.CssSelect("tr").ToArray();
                        if (inner_rows.Count() > 0)
                        {
                            foreach (HtmlNode inner_row in inner_rows)
                            {
                                var inner_cell = inner_row.CssSelect("td").FirstOrDefault();
                                string value = HttpUtility.HtmlDecode(inner_cell.InnerText).Trim();
                                bool add_country = true;
                                foreach (Country c in summ_countries)
                                {
                                    if (c.name == value)
                                    {
                                        add_country = false;
                                        break;
                                    }
                                }
                                if (add_country)
                                {
                                    summ_countries.Add(new Country(value, null));
                                }
                            }
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

        private string GetCountryName(string country_code)
        {
            string country_name = "";
            switch (country_code)
            {
                case "GB":
                    {
                        country_name = "United Kingdom";  break;
                    }
                case "FR":
                    {
                        country_name = "France"; break;
                    }
                case "IT":
                    {
                        country_name = "Italy"; break;
                    }
                case "DE":
                    {
                        country_name = "Germany"; break;
                    }
                case "ES":
                    {
                        country_name = "Spain"; break;
                    }
                case "PT":
                    {
                        country_name = "Portugal"; break;
                    }
                case "BE":
                    {
                        country_name = "Belgium"; break;
                    }
                case "NL":
                    {
                        country_name = "Netherlands"; break;
                    }
                case "DK":
                    {
                        country_name = "Denmark"; break;
                    }
                case "SE":
                    {
                        country_name = "Sweden"; break;
                    }
                case "NO":
                    {
                        country_name = "Norway"; break;
                    }
                case "EE":
                    {
                        country_name = "Estonia"; break;
                    }
                case "FI":
                    {
                        country_name = "Finland"; break;
                    }
                case "PL":
                    {
                        country_name = "Poland"; break;
                    }
                case "RO":
                    {
                        country_name = "Romania"; break;
                    }
                case "CZ":
                    {
                        country_name = "Czechia"; break;
                    }
                case "SK":
                    {
                        country_name = "Slovakia"; break;
                    }
                case "SI":
                    {
                        country_name = "Slovenia"; break;
                    }
                case "BG":
                    {
                        country_name = "Bulgaria"; break;
                    }
                case "CY":
                    {
                        country_name = "Cyprus"; break;
                    }
                case "MT":
                    {
                        country_name = "Malta"; break;
                    }
                case "AT":
                    {
                        country_name = "Austria"; break;
                    }
                case "HR":
                    {
                        country_name = "Croatia"; break;
                    }
                case "GR":
                    {
                        country_name = "Greece"; break;
                    }
                case "HU":
                    {
                        country_name = "Hungary"; break;
                    }
                case "IS":
                    {
                        country_name = "Iceland"; break;
                    }
                case "IE":
                    {
                        country_name = "Ireland"; break;
                    }
                case "LV":
                    {
                        country_name = "Latvia"; break;
                    }
                case "LI":
                    {
                        country_name = "Liechtenstein"; break;
                    }
                case "LT":
                    {
                        country_name = "Lithuania"; break;
                    }
                case "LU":
                    {
                        country_name = "Luxembourg"; break;
                    }
            }
            return country_name;
        }

    }
}
