using HtmlAgilityPack;
using ScrapySharp.Html;
using ScrapySharp.Network;
using ScrapySharp.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;

namespace DataDownloader.isrctn
{
    public class ISRCTN_Processor
    {

        public int GetListLength(WebPage homePage)
        {
            // gets the numbers of records found for the current search
            var resultsHeader = homePage.Find("h1", By.Class("Results_title")).FirstOrDefault();
            string results_string = InnerContent(resultsHeader);
            string results_num = results_string.Substring(0, results_string.IndexOf("results")).Trim();
            if (Int32.TryParse(results_num, out int result_count))
            {
                return result_count;
            }
            else
            {
                return 0;
            }
        }


        public List<ISCTRN_Study> GetPageStudyList(WebPage summaryPage)
        {
            var pageContent = summaryPage.Find("ul", By.Class("ResultsList"));
            HtmlNode[] studyRows = pageContent.CssSelect("li article").ToArray();
            string ISRCTNNumber, remote_link, study_name;
            int colonPos;

            List<ISCTRN_Study> studies = new List<ISCTRN_Study>();
            int n = 0;

            foreach (HtmlNode row in studyRows)
            {
                HtmlNode main = row.CssSelect(".ResultsList_item_main").FirstOrDefault();
                HtmlNode title = main.CssSelect(".ResultsList_item_title a").FirstOrDefault();
                if (title != null)
                {
                    string titleString = title.InnerText?.Replace("\n", "")?.Replace("\r", "")?.Trim() ?? "";
                    if (titleString.Contains(":"))
                    {
                        n++;
                        // get ISRCTN id
                        colonPos = titleString.IndexOf(":");
                        ISRCTNNumber = titleString.Substring(0, colonPos - 1).Trim();
                        study_name = titleString.Substring(colonPos).Trim();
                        remote_link = "https://www.isrctn.com/" + ISRCTNNumber;

                        studies.Add(new ISCTRN_Study(n, ISRCTNNumber, study_name, remote_link));
                    }
                }
            }
            return studies;
        }


        public ISCTRN_Record GetFullDetails(WebPage detailsPage, string ISRCTNNumber)
        {
            ISCTRN_Record st = new ISCTRN_Record();
            st.isctrn_id = ISRCTNNumber;

            // heading data in a header tag

            var titles = detailsPage.Find("header", By.Class("ComplexTitle")).FirstOrDefault();
            st.doi = InnerContent(titles.SelectSingleNode("div[1]/span[2]"));
            st.study_name = InnerContent(titles.SelectSingleNode("div[1]/h1[1]"));

            // Basic metadata in an 'Info_aside' div, split into 2 dl tags
            // each with pairs of dt / dd tags -
            // in each case dt tags identified, value obtained from folowwing sibling

            var meta = detailsPage.Find("div", By.Class("Info_aside")).FirstOrDefault();

            var sub_date = meta.SelectSingleNode("dl[1]/dt[text()='Submission date']");
            st.submission_date = GetDate(sub_date.SelectSingleNode("following-sibling::dd[1]"));
            var reg_date = meta.SelectSingleNode("dl[1]/dt[text()='Registration date']");
            st.registration_date = GetDate(reg_date.SelectSingleNode("following-sibling::dd[1]"));
            var last_edited = meta.SelectSingleNode("dl[1]/dt[text()='Last edited']");
            st.last_edited = GetDate(last_edited.SelectSingleNode("following-sibling::dd[1]"));

            var rec_status = meta.SelectSingleNode("dl[2]/dt[text()='Recruitment status']");
            st.recruitment_status = InnerContent(rec_status.SelectSingleNode("following-sibling::dd[1]"));
            var trial_status = meta.SelectSingleNode("dl[2]/dt[text()='Overall trial status']");
            st.trial_status = InnerContent(trial_status.SelectSingleNode("following-sibling::dd[1]"));
            var cond_cat = meta.SelectSingleNode("dl[2]/dt[text()='Condition category']");
            st.condition_category = InnerContent(cond_cat.SelectSingleNode("following-sibling::dd[1]"));


            List<Item> study_contacts = new List<Item>();
            List<Item> study_identifiers = new List<Item>();
            List<Item> study_info = new List<Item>();
            List<Item> study_eligibility = new List<Item>();
            List<Item> study_locations = new List<Item>();
            List<Item> study_sponsor = new List<Item>();
            List<Item> study_funders = new List<Item>();
            List<Item> study_publications = new List<Item>();
            List<Item> study_additional_files = new List<Item>();
            List<Item> study_notes = new List<Item>();
            List<Output> study_outputs = new List<Output>();

            var section_div = detailsPage.Find("div", By.Class("l-Main")).FirstOrDefault();
            var article = section_div.SelectSingleNode("article[1]");

            var summary_header = article.SelectSingleNode("section[1]/div[1]/div[1]/h3[text()='Plain English Summary']");
            string summary_text = InnerLargeContent(summary_header.SelectSingleNode("following-sibling::p[1]"));
            if (summary_text != "")
            {
                // try and standardise the text, or atr least phrases within it
                // by default the summary text is the whole of the 'Plain English Summary'; but this often
                // contains inform,ation repweeated in a structured form elsewhere.
                // Attempt here is to get the two most relevant sections only.

                summary_text = summary_text.Replace("&nbsp;", " ");
                summary_text = summary_text.Replace("  ", " ");
                summary_text = summary_text.Replace("<br>", "<br/>");
                summary_text = summary_text.Replace("<br/> <br/>", "<br/><br/>");

                bool use_stext = false;
                string stext = summary_text;

                stext = stext.Replace("aims <br/>", "aims<br/>");
                stext = stext.Replace("aims:<br/>", "aims<br/>");
                stext = stext.Replace("aim <br/>", "aims<br/>");
                stext = stext.Replace("aim<br/>", "aims<br/>");

                stext = stext.Replace("study involve? <br/>", "study involve?<br/>");
                stext = stext.Replace("<br/><br/>What are the benefits", "<br/><br/>What are the possible");

                if (stext.Contains("aims<br/>") && stext.Contains("<br/><br/>Who can participate?"))
                {
                    int startpos = stext.IndexOf("aims<br/>") + "aims<br/>".Length;
                    int endpos = stext.IndexOf("<br/><br/>Who can participate?");
                    if (endpos > startpos)
                    {
                        stext = stext.Substring(startpos, endpos - startpos).Trim();
                        use_stext = true;
                    }
                }

                if (stext.Contains("What does the study involve?<br/>") && stext.Contains("<br/><br/>What are the possible"))
                {
                    int startpos = stext.IndexOf("Background and study aims<br/>") + "Background and study aims<br/>".Length;
                    int endpos = stext.IndexOf("<br/><br/>What are the possible benefits");
                    if (endpos > startpos)
                    {
                        stext += stext.Substring(startpos, endpos - startpos);
                    }
                }
                
                st.background = use_stext ? stext : summary_text;
            }

           
            var website_header = article.SelectSingleNode("section[1]/div[1]/div[1]/h3[text()='Trial website']");
            if (website_header != null)
            {
                var website_info = website_header.SelectSingleNode("following-sibling::p[1]");
                st.trial_website = InnerContent(website_info);
                if (website_header.SelectSingleNode("a[1]") != null)
                    st.website_link = website_header.SelectSingleNode("a[1]").Attributes["href"].Value;
            }

            string item_name, item_value;

            var contacts = article
                            .SelectNodes("//section/div[1]/h2[text()='Contact information']/following-sibling::div[1]/h3")?
                            .ToArray();
            if (contacts != null)
            {
                int s = 0; 
                for (int i = 0; i < contacts.Length; i++)
                {
                    item_name = contacts[i].InnerText;

                    if (item_name == "ORCID ID")
                    {
                        item_value = InnerContent(contacts[i].SelectSingleNode("following-sibling::p[1]/a[1]"));
                        if (item_value.StartsWith("http://orcid.org/"))
                        {
                            item_value = item_value.Substring("http://orcid.org/".Length);
                        }
                        if (item_value.StartsWith("https://orcid.org/"))
                        {
                            item_value = item_value.Substring("https://orcid.org/".Length);
                        }
                    }
                    else if (item_name == "Contact details")
                    {
                        item_name = "email_address";
                        item_value = InnerContent(contacts[i].SelectSingleNode("following-sibling::p[1]/a[1]"));
                    }
                    else
                    {
                        item_value = InnerContent(contacts[i].SelectSingleNode("following-sibling::p[1]")); 
                    }

                    if (item_value != "" && item_value != "N/A" && item_value != "Not Applicable" && item_value != "Nil known")
                    {
                        s++;
                        study_contacts.Add(new Item(s, item_name, item_value));
                    }
                }
            }


            var identifiers = article
                            .SelectNodes("//section/div[1]/h2[text()='Additional identifiers']/following-sibling::div[1]/h3")?
                            .ToArray();
            if (identifiers != null)
            {
                int s = 0; 
                for (int i = 0; i < identifiers.Length; i++)
                {
                    item_name = identifiers[i].InnerText;
                    item_value = InnerContent(identifiers[i].SelectSingleNode("following-sibling::p[1]"));

                    if (item_value != "" && item_value != "N/A" && item_value != "Not Applicable" && item_value != "Nil known")
                    {
                        s++; 
                        study_identifiers.Add(new Item(s, item_name, item_value));
                    }
                }
            }


            var info = article
                            .SelectNodes("//section/div[1]/h2[text()='Study information']/following-sibling::div[1]/h3")?
                            .ToArray();
            if (info != null)
            {
                int s = 0; 
                for (int i = 0; i < info.Length; i++)
                {
                    item_name = info[i].InnerText;
                    if (item_name != "Intervention" && item_name != "Secondary outcome measures")
                    {
                        item_value = InnerLargeContent(info[i].SelectSingleNode("following-sibling::p[1]"));

                        if (item_value != "" && item_value != "N/A" && item_value != "Not Applicable" && item_value != "Nil known")
                        {
                            s++; 
                            study_info.Add(new Item(s, item_name, item_value));
                        }
                    }
                }
            }


            var eligibility = article
                            .SelectNodes("//section/div[1]/h2[text()='Eligibility']/following-sibling::div[1]/h3")?
                            .ToArray();
            if (eligibility != null)
            {
                int s = 0; 
                for (int i = 0; i < eligibility.Length; i++)
                {
                    item_name = eligibility[i].InnerText;
                    if (item_name != "Participant inclusion criteria" && item_name != "Participant exclusion criteria")
                    {
                        item_value = InnerLargeContent(eligibility[i].SelectSingleNode("following-sibling::p[1]"));
                        if (item_value != "" && item_value != "N/A" && item_value != "Not Applicable" && item_value != "Nil known")
                        {
                            s++; 
                            study_eligibility.Add(new Item(s, item_name, item_value));
                        }
                    }
                }
            }


            var locations = article
                            .SelectNodes("//section/div[1]/h2[text()='Locations']/following-sibling::div[1]/h3")?
                            .ToArray();
            if (locations != null)
            {
                for (int i = 0; i < locations.Length; i++)
                {
                    int s = 0; 
                    item_name = locations[i].InnerText;
                    if (item_name == "Countries of recruitment")
                    {
                        item_value = InnerContent(locations[i].SelectSingleNode("following-sibling::p[1]"));

                        if (item_value != "" && item_value != "N/A" && item_value != "Not Applicable" && item_value != "Nil known")
                        {
                            s++; 
                            study_locations.Add(new Item(s, item_name, item_value));
                        }
                    }

                    if (item_name == "Trial participating centre")
                    {
                        item_value = InnerContent(locations[i].SelectSingleNode("following-sibling::p[1]/b[1]"));

                        if (item_value != "" && item_value != "N/A" && item_value != "Not Applicable" && item_value != "Nil known")
                        {
                            // N.B get emboldened part is facility title (address portion too variable)
                            s++;
                            study_locations.Add(new Item(s, item_name, item_value));
                        }
                    }
                }
            }


            var sponsor = article
                            .SelectNodes("//section/div[1]/h2[text()='Sponsor information']/following-sibling::div[1]/h3")?
                            .ToArray();
            if (sponsor != null)
            {
                int s = 0; 
                for (int i = 0; i < sponsor.Length; i++)
                {
                    item_name = sponsor[i].InnerText;
                    if (item_name != "Sponsor details" && item_name != "Website")
                    {
                        item_value = InnerContent(sponsor[i].SelectSingleNode("following-sibling::p[1]"));
                        if (item_value != "" && item_value != "N/A" && item_value != "Not Applicable" && item_value != "Nil known")
                        {
                            s++; 
                            study_sponsor.Add(new Item(s,item_name, item_value));
                        }
                    }
                }
            }


            var funders = article
                            .SelectNodes("//section/div[1]/h2[text()='Funders']/following-sibling::div[1]/h3")?
                            .ToArray();
            if (funders != null)
            {
                int s = 0; 
                for (int i = 0; i < funders.Length; i++)
                {
                    item_name = funders[i].InnerText;
                    item_value = InnerContent(funders[i].SelectSingleNode("following-sibling::p[1]"));
                    if (item_value != "" && item_value != "N/A" && item_value != "Not Applicable" && item_value != "Nil known")
                    {
                        s++; 
                        study_funders.Add(new Item(s, item_name, item_value));
                    }
                }
            }


            // <a> references may be included and need to be processed
            var publications = article
                            .SelectNodes("//section/div[1]/h2[text()='Results and Publications']/following-sibling::div[1]/h3")?
                            .ToArray();
            if (publications != null)
            {
                int s = 0;
                HtmlNode output_table = null;
                for (int i = 0; i < publications.Length; i++)
                {
                    item_name = publications[i].InnerText;
                    if (item_name != "Trial outputs")
                    {
                        item_value = InnerLargeContent(publications[i].SelectSingleNode("following-sibling::p[1]"));

                        if (item_value != "" && item_value != "N/A" && item_value != "Not Applicable" && item_value != "Nil known")
                        {
                            s++;
                            study_publications.Add(new Item(s, item_name, item_value));
                        }
                    }
                    else
                    {
                        output_table = publications[i].SelectSingleNode("following-sibling::div[1]/table[1]/tbody[1]");
                    }
                }

                if (output_table != null)
                {
                    var outputs = output_table.SelectNodes("tr")?.ToArray();
                    if (outputs != null)
                    {
                        for (int j = 0; j < outputs.Length; j++)
                        {
                            var this_row = outputs[j];
                            var output_attributes = this_row.SelectNodes("td")?.ToArray();
                            if (output_attributes != null)
                            {
                                string type = InnerContent(output_attributes[0]);
                                string url = output_attributes[0]?.SelectSingleNode("a[1]")?.GetAttributeValue("href");
                                if (!string.IsNullOrEmpty(url))
                                {
                                    if (!url.ToLower().StartsWith("http")) 
                                    {
                                        url = url.StartsWith("/") ? "https://www.isrctn.com" + url : "https://www.isrctn.com/" + url;
                                    }
                                }
                                string dets = InnerContent(output_attributes[1]);
                                DateTime? created = GetDate(output_attributes[2]);
                                DateTime? added = GetDate(output_attributes[3]);
                                string pr_reviewed = InnerContent(output_attributes[4]);
                                string pt_facing = InnerContent(output_attributes[5]);

                                study_outputs.Add(new Output(type, url, dets, created, added, pr_reviewed, pt_facing));
                            }
                        }
                    }
                }
            }


            // <a> references may be included and need to be processed
            var additional_files = article
                            .SelectNodes("//section/div[1]/h2[text()='Additional files']/following-sibling::div[1]/ul/li")?
                            .ToArray();
            if (additional_files != null)
            {
                int s = 0; 
                for (int i = 0; i < additional_files.Length; i++)
                {
                    var node  = additional_files[i].SelectSingleNode("a[1]");

                    item_name = InnerContent(node);
                    string item_text = node.OuterHtml?.Replace("\n", "")?.Replace("\r", "")?.Trim() ?? ""; ;

                    int ref_start = item_text.IndexOf("href=") + 6;
                    int ref_end = item_text.IndexOf("\"", ref_start + 1);
                    string href = item_text.Substring(ref_start, ref_end - ref_start);
                    if (href != "")
                    {
                        s++;
                        href = href.StartsWith("/") ? "https://www.isrctn.com" + href : "https://www.isrctn.com/" + href;
                        study_additional_files.Add(new Item(s, item_name, href));
                    }
                }
            }


            var notes = article
                            .SelectNodes("//section/div[1]/h2[text()='Editorial Notes']/following-sibling::div[1]").FirstOrDefault();
            item_value = InnerLargeContent(notes);
            if (item_value != "" && item_value != "N/A" && item_value != "Not Applicable" && item_value != "Nil known")
            {
                int s = 1; 
                study_notes.Add(new Item(s, "study_notes", item_value));
            }


            st.contacts = study_contacts;
            st.identifiers = study_identifiers;
            st.study_info = study_info;
            st.eligibility = study_eligibility;
            st.locations = study_locations;
            st.sponsor = study_sponsor;
            st.funders = study_funders;
            st.publications = study_publications;
            st.additional_files = study_additional_files;
            st.notes = study_notes;
            st.outputs = study_outputs;

            return st;
        }

        public string InnerContent(HtmlNode node)
        {
            if (node == null)
            {
                return "";
            }
            else
            {
                string allInner = node.InnerText?.Replace("\n", "")?.Replace("\r", "")?.Trim() ?? "";
                return HttpUtility.HtmlDecode(allInner);
            }
        }


        public string InnerLargeContent(HtmlNode node)
        {
            if (node == null)
            {
                return "";
            }
            else
            {
                string allInner = node.InnerHtml?.Replace("\n", "")?.Replace("\r", "")?.Trim() ?? "";
                return HttpUtility.HtmlDecode(allInner);
            }
        }


        public DateTime? GetDate(HtmlNode node)
        {
            string date = node.InnerText?.Replace("\n", "")?.Replace("\r", "")?.Trim() ?? "";
            if (DateTime.TryParseExact(date, "dd/MM/yyyy", CultureInfo.InvariantCulture, 
                DateTimeStyles.None, out DateTime dt_value))
            {
                return dt_value;
            }
            else
            {
                return null;
            }
        }
    }
}
