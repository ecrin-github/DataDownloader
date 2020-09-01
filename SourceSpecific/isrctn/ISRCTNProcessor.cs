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
using System.Globalization;

namespace DataDownloader.isrctn
{
    public class ISRCTN_Processor
    {

        public void GetStudyDetails(ScrapingBrowser browser, WebPage homePage, LoggingDataLayer logging_repo, 
                                    int pagenum, string file_base, int saf_id)
        {

            // gets the details of each trial registry record
            // listed on the search page (n=100)

            int n = (pagenum - 1) * 100;
            var pageContent = homePage.Find("ul", By.Class("ResultsList"));
            HtmlNode[] studyRows = pageContent.CssSelect("li article").ToArray();

            List<StudyFileRecord> file_records = new List<StudyFileRecord>();
            string ISRCTNNumber, remote_link;
            int colonPos;

            foreach (HtmlNode row in studyRows)
            {
                n++;
                if (n % 10 == 0) System.Threading.Thread.Sleep(2000);  // pause every 10 accesses

                ISCTRN_Record st = new ISCTRN_Record();
                st.id = n;

                HtmlNode main = row.CssSelect(".ResultsList_item_main").FirstOrDefault();
                HtmlNode title = main.CssSelect(".ResultsList_item_title a").FirstOrDefault();
                if (title != null)
                {
                    string titleString = title.InnerText?.Replace("\n", "")?.Replace("\r", "")?.Trim() ?? "";
                    if (titleString.Contains(":"))
                    {
                        colonPos = titleString.IndexOf(":");
                        ISRCTNNumber = titleString.Substring(0, colonPos - 1).Trim();
                        st.isctrn_id = ISRCTNNumber;
                        remote_link = "https://www.isrctn.com/" + ISRCTNNumber;
                        WebPage detailsPage = browser.NavigateToPage(new Uri(remote_link));
                        GetFullDetails(ref st, detailsPage, logging_repo, pagenum, file_base);

                        // study object should now have properties adde to it
                        // Write out study record as XML

                        string file_name = st.isctrn_id + ".xml";
                        string full_path = Path.Combine(file_base, file_name);
                        System.Xml.Serialization.XmlSerializer writer = new System.Xml.Serialization.XmlSerializer(typeof(ISCTRN_Record));
                        FileStream file = System.IO.File.Create(full_path);
                        writer.Serialize(file, st);
                        file.Close();

                        // create new file record 
                        file_records.Add(new StudyFileRecord(100126, st.isctrn_id, remote_link, saf_id, st.last_edited, full_path));
                    }
                }
            }

            // store file records for the group, usually 100, from this page
            logging_repo.StoreStudyRecs(LoggingCopyHelper.file_record_copyhelper, file_records);

            //repo.StoreDatasetProperties(CopyHelpers.dataset_properties_helper,
                                       //  s.dataset_properties);
        }


        public void GetFullDetails(ref ISCTRN_Record st, WebPage detailsPage, LoggingDataLayer logging_repo,
                                    int pagenum, string file_base)
        {

            var titles = detailsPage.Find("header", By.Class("ComplexTitle")).FirstOrDefault();
            st.doi = InnerContent(titles.SelectSingleNode("div[1]/span[2]"));
            st.study_name = InnerContent(titles.SelectSingleNode("div[1]/h1[1]"));

            var meta = detailsPage.Find("div", By.Class("Info_aside")).FirstOrDefault();

            var condition = meta.SelectSingleNode("dl[1]/dt[text()='Condition category']");
            st.condition_category = InnerContent(condition.SelectSingleNode("following-sibling::dd[1]"));
            var assigned = meta.SelectSingleNode("dl[1]/dt[text()='Date assigned']");
            st.date_assigned = GetDate(assigned.SelectSingleNode("following-sibling::dd[1]"));
            var edited = meta.SelectSingleNode("dl[1]/dt[text()='Last edited']");
            st.last_edited = GetDate(edited.SelectSingleNode("following-sibling::dd[1]"));


            var reg_type = meta.SelectSingleNode("dl[2]/dt[text()='Prospective/Retrospective']");
            st.registration_type = InnerContent(reg_type.SelectSingleNode("following-sibling::dd[1]"));
            var trial_status = meta.SelectSingleNode("dl[2]/dt[text()='Overall trial status']");
            st.trial_status = InnerContent(trial_status.SelectSingleNode("following-sibling::dd[1]"));
            var recruitment_status = meta.SelectSingleNode("dl[2]/dt[text()='Recruitment status']");
            st.recruitment_status = InnerContent(recruitment_status.SelectSingleNode("following-sibling::dd[1]"));

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

            var section_div = detailsPage.Find("div", By.Class("l-Main")).FirstOrDefault();

            var summary_header = section_div.SelectSingleNode("article[1]/section[1]/div[1]/div[1]/h3[text()='Plain English Summary']");
            string summary_text = InnerLargeContent(summary_header.SelectSingleNode("following-sibling::p[1]"));
            if (summary_text != "")
            {
                summary_text = summary_text.Replace("<br>", "<br/>");
                summary_text = summary_text.Replace("study aims <br/>", "study aims<br/>");
                
                if (summary_text.Contains("Background and study aims<br/>") && summary_text.Contains("<br/><br/>Who can participate?"))
                {
                    int startpos = "Background and study aims<br/>".Length;
                    int endpos = summary_text.IndexOf("<br/><br/>Who can participate?");
                    summary_text = summary_text.Substring(startpos, endpos - startpos);
                    
                }
                st.background = summary_text;
            }

            var website_header = section_div.SelectSingleNode("article[1]/section[1]/div[1]/div[1]/h3[text()='Trial website']");
            if (website_header != null)
            {
                var website_info = website_header.SelectSingleNode("following-sibling::p[1]");
                st.trial_website = InnerContent(website_info);
                if (website_header.SelectSingleNode("a[1]") != null)
                    st.website_link = website_header.SelectSingleNode("a[1]").Attributes["href"].Value;
            }

            string item_name, item_value;

            var contacts = section_div
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


            var identifiers = section_div
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


            var info = section_div
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


            var eligibility = section_div
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


            var locations = section_div
                            .SelectNodes("//section/div[1]/h2[text()='Locations']/following-sibling::div[1]/h3")?
                            .ToArray();
            if (locations != null)
            {
                for (int i = 0; i < locations.Length; i++)
                {
                    int s = 0; 
                    item_name = locations[i].InnerText;
                    if (item_name != "Trial participating centre")
                    {
                        item_value = InnerContent(locations[i].SelectSingleNode("following-sibling::p[1]"));
                        if (item_value != "" && item_value != "N/A" && item_value != "Not Applicable" && item_value != "Nil known")
                        {
                            s++; 
                            study_locations.Add(new Item(s, item_name, item_value));
                        }
                    }
                }
            }


            var sponsor = section_div
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


            var funders = section_div
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
            var publications = section_div
                            .SelectNodes("//section/div[1]/h2[text()='Results and Publications']/following-sibling::div[1]/h3")?
                            .ToArray();
            if (publications != null)
            {
                int s = 0; 
                for (int i = 0; i < publications.Length; i++)
                {
                    item_name = publications[i].InnerText;

                    item_value = InnerLargeContent(publications[i].SelectSingleNode("following-sibling::p[1]"));
                    if (item_value != "" && item_value != "N/A" && item_value != "Not Applicable" && item_value != "Nil known")
                    {
                        s++; 
                        study_publications.Add(new Item(s, item_name, item_value));
                    }

                }
            }

            // <a> references may be included and need to be processed
            var additional_files = section_div
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
                        study_additional_files.Add(new Item(s, item_name, "https://www.isrctn.com/" + href));
                    }
                }
            }


            var notes = section_div
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
