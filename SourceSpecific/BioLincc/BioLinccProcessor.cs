﻿using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Html;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataDownloader.biolincc
{
    public class BioLINCC_Processor
    {
        public BioLincc_Basics GetStudyBasics(HtmlNode row)
        {

            // get the basic information from the summary table row
            // 4 columns in each row, first of which has a link
            // the second the acronym, the third the summary of resources and 
            // the fourth the collection type
            // Note the use of the acronym as the sd_sid 

            HtmlNode[] cols = row.CssSelect("td").ToArray();
            BioLincc_Basics bb = new BioLincc_Basics();

            HtmlNode link = cols[0].CssSelect("a").FirstOrDefault();
            bb.title = link.InnerText.Trim();
            bb.remote_url = "https://biolincc.nhlbi.nih.gov" + link.Attributes["href"].Value;
            bb.acronym = cols[1].InnerText.Replace("\n", "").Replace("\r", "").Trim();
            bb.resources_available = cols[2].InnerText.Replace("\n", "").Replace("\r", "").Trim();
            bb.collection_type = cols[3].InnerText.Replace("\n", "").Replace("\r", "").Trim();
            bb.sd_sid = bb.acronym.Replace("\\", "-").Replace("/", "-").Replace(".", "-");

            return bb;
        }


        public BioLincc_Record GetStudyDetails(BioLincc_Basics bb, ScrapingHelpers ch, BioLinccDataLayer repo, LoggingHelper logging_helper)
        {
            // First check the study main page can be reached
            DateHelpers dh = new DateHelpers(logging_helper);
            WebPage studyPage = ch.GetPage(bb.remote_url);
            if (studyPage == null)
            {
                logging_helper.LogError("Attempt to access main BioLInnc study page for " + bb.acronym + " failed");
                return null;
            }

            // Set up

            List<PrimaryDoc> primary_docs = new List<PrimaryDoc>();
            List<RegistryId> registry_ids = new List<RegistryId>();
            List<Resource> study_resources = new List<Resource>();
            List<AssocDoc> assoc_docs = new List<AssocDoc>();
            string nct_id = "";
            char[] splitter = { '(' };

            BioLincc_Record st = new BioLincc_Record(bb);

            // Main page components.

            var main = studyPage.Find("div", By.Class("main"));
            var tables = main.CssSelect("div.study-box").ToArray();
            var table1 = tables[0];
            var table2 = tables[1];
            var sideBar = main.CssSelect("div.col-md-3");
            var descriptive_paras = studyPage.Find("div", By.Id("study-info")).FirstOrDefault();
            var description = descriptive_paras.CssSelect("div.col-sm-12").FirstOrDefault();

            // Scan top table .

            HtmlNode[] entries = table1.CssSelect("p").ToArray();
            for (int i = 0; i < entries.Count(); i++)
            {
                HtmlNode entryBold = entries[i].CssSelect("b").FirstOrDefault();
                HtmlNode entrySupp = entries[i].CssSelect("em").FirstOrDefault();
                HtmlNode[] entryRefs = entries[i].CssSelect("a").ToArray();
                string attribute_name = entryBold.InnerText.Trim();
                string attribute_value = entries[i].InnerText;

                if (attribute_name == "Accession Number") st.accession_number = ch.AttValue(attribute_value, attribute_name, entrySupp);
                if (attribute_name == "Study Type")
                {
                    string study_type = ch.AttValue(attribute_value, attribute_name, entrySupp);
                    if (study_type.Contains("Clinical Trial"))
                    {
                        st.study_type_id = 11;
                        st.study_type = "Interventional";
                    }
                    else if (study_type == "Epidemiology Study")
                    {
                        st.study_type_id = 12;
                        st.study_type = "Observational";
                    }
                    else
                    {
                        st.study_type_id = 0;
                        st.study_type = "Not yet known";
                    }
                }

                if (attribute_name == "Study Period") st.study_period = ch.AttValue(attribute_value, attribute_name, entrySupp);

                if (attribute_name == "Date Prepared")
                {
                    st.date_prepared = ch.AttValue(attribute_value, attribute_name, entrySupp);
                    if (st.date_prepared == "N/A" || string.IsNullOrEmpty(st.date_prepared))
                    {
                        st.page_prepared_date = null;
                    }
                    else
                    {
                        // date is in the forem of MMMM d, yyyy, and needs to be split accordingly
                        string date_prepared_string = st.date_prepared.Replace(", ", "|").Replace(",", "|").Replace(" ", "|");
                        string[] updated_parts = date_prepared_string.Split("|");
                        int month = dh.GetMonthAsInt(updated_parts[0]);
                        if (month > 0
                            && Int32.TryParse(updated_parts[1], out int day)
                            && Int32.TryParse(updated_parts[2], out int year))
                        {
                            st.page_prepared_date = new DateTime(year, month, day);
                        }
                    }
                }


                if (attribute_name == "Dataset(s) Last Updated")
                {
                    st.last_updated = ch.AttValue(attribute_value, attribute_name, entrySupp);
                    if (st.last_updated == "N/A" || string.IsNullOrEmpty(st.last_updated))
                    {
                        st.last_revised_date = null;
                    }
                    else
                    {
                        // date is in the forem of MMMM d, yyyy, and needs to be split accordingly
                        string last_updated_string = st.last_updated.Replace(", ", "|").Replace(",", "|").Replace(" ", "|");
                        string[] updated_parts = last_updated_string.Split("|");
                        int month = dh.GetMonthAsInt(updated_parts[0]);
                        if (month > 0
                            && Int32.TryParse(updated_parts[1], out int day)
                            && Int32.TryParse(updated_parts[2], out int year))
                        {
                            st.last_revised_date = new DateTime(year, month, day);
                        }
                    }
                }

                /*
                // can no longer use - both dates have disappeared
                // st,publication year remains null;
                // the only date is the date the datasets last updated
                // now corresponds to st.last_updated

                if (st.page_prepared_date != null)
                {
                    st.publication_year = ((DateTime)st.page_prepared_date).Year;
                }
                else if (st.last_revised_date != null)
                {
                    st.publication_year = ((DateTime)st.last_revised_date).Year;
                }
                */

                if (attribute_name == "Clinical Trial URLs")
                {
                    st.num_clinical_trial_urls = entryRefs.Count();

                    // comment starts out as all of the inner text
                    string comment = entries[i].InnerText.Replace(attribute_name, "").Trim();

                    // remove the inner text of the <a> elements, the entryRefs
                    if (entryRefs.Count() > 0)
                    {
                        for (int j = 0; j < entryRefs.Count(); j++)
                        {
                            comment = comment.Replace(entryRefs[j].InnerText, "");
                        }
                    }

                    // split the remainer using the '(' character, removing empty entries
                    string[] ctcomments = (comment != "") ? comment.Split(splitter, System.StringSplitOptions.RemoveEmptyEntries) : null;
                    if (ctcomments != null)
                    {
                        // lose the first 'comment'
                        ctcomments = (ctcomments.Count() == 1) ? null : ctcomments.Skip(1).ToArray();
                    }

                    if (entryRefs.Count() > 0)
                    {
                        string this_comm = "";
                        for (int j = 0; j < entryRefs.Count(); j++)
                        {
                            // get the url
                            attribute_value = entryRefs[j].Attributes["href"].Value.Trim();

                            // if there are any comments add the corrresponding one for this url
                            if (ctcomments != null)
                            {
                                this_comm = (j < ctcomments.Count()) ? "(" + ctcomments[j].Replace("\n", "").Replace("\r", "").Trim() : "";
                            }

                            // derive NCT number
                            int NCTPos = attribute_value.ToUpper().IndexOf("NCT");
                            if (NCTPos > -1 && NCTPos <= attribute_value.Length - 11)
                            {
                                nct_id = attribute_value.Substring(NCTPos, 11).ToUpper();
                            }
                            else
                            {
                                nct_id = "Unknown";
                            }

                            registry_ids.Add(new RegistryId(attribute_value, nct_id, this_comm));
                        }
                    }
                }

                if (attribute_name == "Primary Publication URLs")
                {
                    st.num_primary_pub_urls = entryRefs.Count();

                    // constructy comment by first losing the atribute name and then all
                    // the text in the references
                    string comment = entries[i].InnerText.Replace(attribute_name, "").Trim();
                    if (entryRefs.Count() > 0)
                    {
                        for (int j = 0; j < entryRefs.Count(); j++)
                        {
                            comment = comment.Replace(entryRefs[j].InnerText, "");
                        }
                    }

                    // split the remaining text on the "(" and the lose the first one
                    string[] pdcomments = (comment != "") ? comment.Split(splitter, System.StringSplitOptions.RemoveEmptyEntries) : null;
                    if (pdcomments != null)
                    {
                        pdcomments = (pdcomments.Count() == 1) ? null : pdcomments.Skip(1).ToArray();
                    }

                    // get each primary publication listed
                    if (entryRefs.Count() > 0)
                    {
                        string pubmed_id = ""; int pubmed_pos;
                        string this_comm = "";
                        for (int j = 0; j < entryRefs.Count(); j++)
                        {
                            attribute_value = entryRefs[j].Attributes["href"].Value.Trim();

                            // get pubmed id
                            pubmed_pos = attribute_value.IndexOf("/pubmed/");
                            if (pubmed_pos != -1)
                            {
                                pubmed_id = attribute_value.Substring(pubmed_pos + 8);
                            }
                            else
                            {
                                // need to interrogate NLM API 
                                int pmc_pos = attribute_value.IndexOf("/pmc/articles/");
                                if (pmc_pos != -1)
                                {
                                    string pmc_id = attribute_value.Substring(pmc_pos + 14);
                                    pmc_id = pmc_id.Replace("/", "");
                                    pubmed_id = ch.GetPMIDFromNLM(pmc_id);
                                }
                            }

                            // if there are any comments add the corrresponding one for this refernce
                            if (pdcomments != null)
                            {
                                this_comm = (j < pdcomments.Count()) ? "(" + pdcomments[j].Replace("\n", "").Replace("\r", "").Trim() : "";
                            }

                            PrimaryDoc primaryDoc = new PrimaryDoc(attribute_value, pubmed_id, this_comm);
                            primary_docs.Add(primaryDoc);

                        }
                    }
                }

                if (attribute_name == "Study Website")
                {
                    if (entryRefs.Count() > 0)
                    {
                        st.study_website = entryRefs[0].Attributes["href"].Value;
                    }
                }
            }


            // scan descriptive paragraphs for objectives and background
            HtmlNode[] desc_headings = description.CssSelect("h2").ToArray();
            if (desc_headings.Count() > 0)
            {
                // first identify start point
                string startpoint_text = "";
                for (int i = 0; i < desc_headings.Count(); i++)
                {
                    string desc_header = desc_headings[i].InnerText.Trim();
                    if (desc_header == "Objectives")
                    {
                        startpoint_text = "<h2 class=\"study-info-heading\">Objectives";
                        break;
                    }
                }

                if (startpoint_text != "")
                {
                    string descriptive_text = description.InnerHtml;
                    int start_point = descriptive_text.IndexOf(startpoint_text);
                    descriptive_text = descriptive_text.Substring(start_point);

                    // then identify cut-off point...
                    string cutoff_text = "";
                    for (int i = 0; i < desc_headings.Count(); i++)
                    {
                        string desc_header = desc_headings[i].InnerText.Trim();
                        if (desc_header == "Background")
                        {
                            cutoff_text = "<h2 class=\"study-info-heading\">Background";
                            break;
                        }
                        if (desc_header == "Subjects")
                        {
                            cutoff_text = "<h2 class=\"study-info-heading\">Subjects";
                            break;
                        }
                        if (desc_header == "Design")
                        {
                            cutoff_text = "<h2 class=\"study-info-heading\">Design";
                            break;
                        }
                    }

                    if (cutoff_text != "")
                    {
                        int cutoff_point = descriptive_text.IndexOf(cutoff_text);
                        descriptive_text = descriptive_text.Substring(0, cutoff_point);
                    }

                    descriptive_text = descriptive_text.Replace("<!-- study description column -->", "").Trim();
                    descriptive_text = descriptive_text.Replace("\n", "").Replace("\r", "").Trim();
                    descriptive_text = descriptive_text.Replace(">Objectives<", "Objectives: ");
                    descriptive_text = descriptive_text.Replace("<p>", "\n").Replace("</p>", "\n").Replace("<br />", "\n").Replace("<br>", "\n");
                    descriptive_text = descriptive_text.Replace("<b>", "").Replace("</b>", "").Replace("<em>", "").Replace("</em>", "");
                    descriptive_text = descriptive_text.Replace("<i>", "").Replace("</i>", "").Replace("<u>", "").Replace("</u>", "");
                    descriptive_text = descriptive_text.Replace("/h2>", "").Replace("<h2 class=\"study-info-heading\"", "");
                    descriptive_text = descriptive_text.Replace("    ", "").Replace("   ", "").Replace("  ", "");
                    descriptive_text = descriptive_text.Replace("\n\n", "\n");
                    descriptive_text = descriptive_text.Replace("Objectives: \n", "Objectives: ");
                    st.brief_description = descriptive_text;
                }
            }


            // consent restrictions
            if (st.resources_available.Contains("Study Datasets"))
            {
                bool comm_use_data_restrics = false;
                bool data_restrics_based_on_aor = false;
                string specific_consent_restrics = "";

                HtmlNode[] entries2 = table2.CssSelect("p").ToArray();
                for (int i = 0; i < entries2.Count(); i++)
                {
                    HtmlNode entryBold = entries2[i].CssSelect("b").FirstOrDefault();
                    HtmlNode entrySupp = entries2[i].CssSelect("em").FirstOrDefault();

                    string attribute_name = entryBold.InnerText.Trim();
                    string attribute_value = entries2[i].InnerText;

                    if (attribute_name == "Commercial Use Data Restrictions")
                    {
                        string comm_use_restrictions = ch.AttValue(attribute_value, attribute_name, entrySupp);
                        if (comm_use_restrictions != null)
                        {
                            comm_use_data_restrics = (comm_use_restrictions.ToLower() == "yes") ? true : false; ;
                        }
                    }

                    if (attribute_name == "Data Restrictions Based On Area Of Research")
                    {
                        string aor_use_restrictions = ch.AttValue(attribute_value, attribute_name, entrySupp);
                        if (aor_use_restrictions != null)
                        {
                            data_restrics_based_on_aor = (aor_use_restrictions.ToLower() == "yes") ? true : false;
                        }
                    }

                    if (attribute_name == "Specific Consent Restrictions") specific_consent_restrics = ch.AttValue(attribute_value, attribute_name, entrySupp);
                }

                // for the datasets, construct any consent constraints
                string restrictions = "";

                if (comm_use_data_restrics && data_restrics_based_on_aor)
                {
                    restrictions += "Restrictions reported on use of data for commercial purposes, and depending on the area of research. ";
                }
                else if (data_restrics_based_on_aor)
                {
                    restrictions += "Restrictions reported on the use of data depending on the area of research. ";
                }
                else if (comm_use_data_restrics)
                {
                    restrictions += "Restrictions reported on use of data for commercial purposes. ";
                }

                if (!string.IsNullOrEmpty(specific_consent_restrics))
                {
                    restrictions += specific_consent_restrics;
                }

                if (restrictions != "")
                {
                    st.dataset_consent_type_id = 9;
                    st.dataset_consent_type = "Not classified but comment on consent present";
                }
                else
                {
                    st.dataset_consent_type_id = 0;
                    st.dataset_consent_type = "Not yet known";
                }
                st.dataset_consent_restrictions = restrictions;
            }


            // side bar
            HtmlNode[] sections = sideBar.CssSelect("div.detail-aside-row").ToArray();
            List<Link> Links = new List<Link>();

            for (int i = 0; i < sections.Count(); i++)
            {
                HtmlNode headerLine = sections[i].CssSelect("h2").FirstOrDefault();
                if (headerLine != null)
                {
                    string headerText = headerLine?.InnerText?.Trim();
                    if (headerText != "Study Catalog")
                    {
                        if (headerText == "Resources Available")
                        {
                            string att_value = sections[i].InnerText.Replace("Resources Available", "");
                            // replace original value from table as this is slightly more descriptive
                            st.resources_available = att_value.Replace("\n", "").Replace("\r", "").Trim();
                        }

                        if (headerText.Length > 18 && headerText.Substring(0, 18) == "Study Publications")
                        {
                            // just record the link to the index page for now
                            // the number of publications will be collected later
                            HtmlNode refNode = headerLine.CssSelect("a").FirstOrDefault();
                            Links.Add(new Link("Study Publications", refNode.Attributes["href"].Value));
                        }

                        if (headerText == "Study Documents")
                        {
                            HtmlNode[] documents = sections[i].CssSelect("ul a").ToArray();
                            string doc_name, doc_type, object_type, url, size, sizeUnits;
                            int? object_type_id, doc_type_id, access_type_id;

                            foreach (HtmlNode node in documents)
                            {
                                // re-initialise
                                doc_name = ""; doc_type = ""; object_type = ""; url = ""; size = ""; sizeUnits = "";
                                object_type_id = 0; doc_type_id = 0; access_type_id = 0;

                                // get the url for the document
                                url = node.Attributes["href"].Value;
                                // add site prefix and chop off time stamp parameter, if one has been added
                                url = "https://biolincc.nhlbi.nih.gov" + url;
                                if (url.IndexOf("?") > 0) url = url.Substring(0, url.IndexOf("?"));

                                string docString = node.InnerText.Trim();

                                // split off the bracketed data on type and size
                                string[] brackets = docString.Split(splitter);  // split on left bracket
                                if (brackets.Count() == 1)
                                {
                                    // no bracket (not sure if it occurs)
                                    doc_name = docString.Trim();
                                    object_type = "List of web links";
                                    object_type_id = 86;
                                    doc_type = "Web text";
                                    doc_type_id = 35;
                                    access_type_id = 12;
                                    sizeUnits = "";
                                    size = "";
                                }

                                if (brackets.Count() > 1)
                                {
                                    doc_name = brackets[0].Trim();
                                    // in case there is bracketed text in the name - assumed not more than one such
                                    if (brackets.Count() == 3) doc_name = (brackets[0] + "(" + brackets[1]).Trim();

                                    string docInfo = brackets[brackets.Count() - 1].Trim();  // get last bracketed text, normally will just be one
                                    docInfo = docInfo.Substring(0, docInfo.Length - 1);  // drop right bracket

                                    string[] parameters = docInfo.Split('-');  // split on hyphen, assumed this is a constant feature
                                    if (parameters.Count() == 1)
                                    {
                                        // no hyphen
                                        doc_type = docInfo.Trim();
                                        sizeUnits = "";
                                        size = "";
                                    }

                                    if (parameters.Count() > 1)
                                    {
                                        doc_type = parameters[0].Trim();

                                        string sizeString = parameters[1].Replace('\u00A0', '\u0020').Trim();  // replace any non breaking spaces with spaces
                                        string[] sizePars = sizeString.Split(' '); // split on space
                                        if (sizePars.Count() == 1)
                                        {
                                            // no space
                                            sizeUnits = sizeString.Trim();
                                            size = "";
                                        }
                                        if (sizePars.Count() > 1)
                                        {
                                            size = sizePars[0].Trim();
                                            sizeUnits = sizePars[1].Trim();
                                        }
                                    }
                                }

                                // identify resource type in MDR terms

                                // first obtain object and doc types where straightforward 
                                if (doc_type == "PDF")
                                {
                                    doc_type_id = 11;
                                    access_type_id = 11;
                                }
                                if (doc_type == "HTM")
                                {
                                    object_type = "List of web links";
                                    object_type_id = 86;
                                    doc_type = "Web text";
                                    doc_type_id = 35;
                                    access_type_id = 12;
                                }

                                if (object_type_id == 0)
                                {
                                    // code the common ones
                                    switch (doc_name)
                                    {
                                        case "Data Dictionary":
                                            {
                                                object_type = "Data Dictionary";
                                                object_type_id = 31;
                                                break;
                                            }
                                        case "Protocol":
                                            {
                                                object_type = "Study Protocol";
                                                object_type_id = 11;
                                                break;
                                            }
                                        case "Manual of Operations":
                                            {
                                                object_type = "Manual of Operations";
                                                object_type_id = 35;
                                                break;
                                            }
                                        case "Manual of Procedures":
                                            {
                                                object_type = "Manual of Procedures";
                                                object_type_id = 36;
                                                break;
                                            }
                                        case "Forms":
                                            {
                                                object_type = "Data collection forms";
                                                object_type_id = 21;
                                                break;
                                            }
                                    }

                                    // for all those left call into the database table 
                                    if (object_type_id == 0)
                                    {
                                        ObjectTypeDetails object_type_details = repo.FetchDocTypeDetails(doc_name);
                                        if (object_type_details?.type_id != null)
                                        {
                                            object_type_id = object_type_details.type_id;
                                            object_type = object_type_details.type_name;
                                        }
                                        else
                                        {
                                            logging_helper.LogLine("!!!! Need to map " + doc_name + " in pp.document_types table !!!!");
                                            st.UnmatchedDocTypes.Add(doc_name);
                                        }
                                    }
                                }

                                study_resources.Add(new Resource(doc_name, object_type_id, object_type, doc_type_id, doc_type,
                                                                     access_type_id, url, size, sizeUnits));
                            }
                        }
                    }
                }
            }

            #region associated publications data
            // Associated publications
            // leave out for now

            if (Links.Count > 0)
            {
                string pubURL = "https://biolincc.nhlbi.nih.gov" + Links[0].url + "&page_size=200";
                WebPage pubsPage = ch.GetPage(pubURL);
                if (pubsPage == null)
                {
                    logging_helper.LogError("Attempt to access study links page for " + bb.acronym + " failed");
                }
                else
                {
                    var pubTable = pubsPage.Find("div", By.Class("table-responsive"));
                    HtmlNode[] pubLinks = pubTable.CssSelect("td a").ToArray();
                    st.num_associated_papers = pubLinks.Count();

                    string pubNodeId = "";
                    foreach (HtmlNode pubnode in pubLinks)
                    {
                        pubNodeId = pubnode.Attributes["href"].Value;
                        Links.Add(new Link(pubNodeId, "https://biolincc.nhlbi.nih.gov/publications/" + pubNodeId));
                    }

                    // get the details of each listed publication
                    // Links[0] has the link to the publications page, so start at 1
                    string att_value = "";
                    for (int i = 1; i < Links.Count(); i++)
                    {
                        System.Threading.Thread.Sleep(600);
                        WebPage pubsDetailsPage = ch.GetPage(Links[i].url);
                        if (pubsDetailsPage == null)
                        {
                            logging_helper.LogError("Attempt to access specific study link details for " + bb.acronym + " failed");
                        }
                        else
                        {
                            // set up publication record
                            AssocDoc pubdets = new AssocDoc(Links[i].attribute);

                            var mainData = pubsDetailsPage.Find("div", By.Class("main"));

                            // Get the title
                            HtmlNode pubTitle = mainData.CssSelect("h1 b").FirstOrDefault();
                            pubdets.title = pubTitle.InnerText.Trim();

                            // Other available details
                            HtmlNode[] pubData = mainData.CssSelect("p").ToArray();

                            foreach (HtmlNode node in pubData)
                            {
                                HtmlNode inBold = node.CssSelect("b").FirstOrDefault();
                                if (inBold != null)
                                {
                                    string attType = inBold.InnerText.Trim();

                                    att_value = node.InnerText.Replace(attType, "");
                                    att_value = att_value.Replace("\n", "").Replace("\r", "").Trim();

                                    if (attType.EndsWith(":")) attType = attType.Substring(0, attType.Count() - 1);

                                    switch (attType)
                                    {
                                        case "Pubmed ID":
                                            {
                                                pubdets.pubmed_id = att_value;
                                                break;
                                            }
                                        case "Pubmed Central ID":
                                            {
                                                pubdets.pmc_id = att_value;
                                                break;
                                            }
                                        case "Cite As":
                                            {
                                                pubdets.display_title = att_value;
                                                break;
                                            }
                                        case "Journal":
                                            {
                                                pubdets.journal = att_value;
                                                break;
                                            }
                                        case "Publication Date":
                                            {
                                                pubdets.pub_date = att_value;
                                                break;
                                            }
                                    }
                                }
                            }

                            assoc_docs.Add(pubdets);
                        }
                    }
                }
            }

            #endregion

            // get sponsor details from linked NCT record
            // (or first one listed if multiple).

            if (registry_ids.Count > 0)
            {
                string NCTId = registry_ids[0].nct_id;
                var sponsor_details = repo.FetchSponsorFromNCT(NCTId);
                if (sponsor_details != null)
                {
                    st.sponsor_id = sponsor_details.org_id;
                    st.sponsor_name = sponsor_details.org_name;
                }
                st.nct_base_name = repo.FetchNameBaseFromNCT(NCTId);
            }

            st.primary_docs = primary_docs;
            st.registry_ids = registry_ids;
            st.resources = study_resources;
            st.assoc_docs = assoc_docs;

            return st;
        }
    }
}
