using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Html;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DataDownloader.yoda
{
    public class Yoda_Processor
    {
        public List<Summary> GetStudyInitialDetails(WebPage homePage, int pagenum, StringHelpers sh)
        {
            string cellValue = "";
            var pageContent = homePage.Find("div", By.Class("view-content"));
            HtmlNode[] studyRows = pageContent.CssSelect("tbody tr").ToArray();
            List<Summary> page_study_list = new List<Summary>();
         
            foreach (HtmlNode row in studyRows)
            {
                // 6 columns in each row, 
                // 0: NCT number, 1: generic name, 2: title, with link 3: Enrolment number, 
                // 4: CSR download link, 5: view field ops (?)

                Summary sm = new Summary();
                HtmlNode[] cols = row.CssSelect("td").ToArray();
                for (int i = 0; i < 5; i++)
                {
                    HtmlNode col = cols[i];
                    cellValue = col.InnerText?.Replace("\n", "")?.Replace("\r", "")?.Trim() ?? "";
                    if (cellValue != "")
                    {
                        cellValue = cellValue.Replace("??", " ").Replace("&#039;", "’").Replace("'", "’");
                    }
                    switch (i)
                    {
                        case 0: sm.registry_id = cellValue; break;   // Usually NCT number, may be an ISRCT id, may be blank
                        case 1: sm.generic_name = cellValue; break;
                        case 2:
                            {
                                sm.study_name = cellValue;
                                HtmlNode link = col.CssSelect("a").FirstOrDefault();
                                string link_text = link.Attributes["href"].Value;
                                sm.details_link = "https://yoda.yale.edu" + link_text; // url for details page.
                                break;
                            }
                        case 3:
                            {
                                if (cellValue != "") cellValue = cellValue.Replace(" ", "");
                                sm.enrolment_num = cellValue;
                                break;
                            }
                        case 4:
                            {
                                if (cellValue != "")
                                {
                                    HtmlNode link = col.CssSelect("a").FirstOrDefault();
                                    sm.csr_link = link.Attributes["href"].Value;
                                }
                                else
                                {
                                    sm.csr_link = "";
                                }
                                break;
                            }
                    }
                   
                    // obtain an sd_id as an MD5 hash of the title, as listed in Yoda
                    // which is the full scientific title - but these are not unique (7 duplicted at least)
                    // so prefix with registry id and enrolment numbers 
                    // - this combination does seem to be unique

                    if (sm.registry_id.StartsWith("NCT") || sm.registry_id.StartsWith("ISRCTN"))
                    {
                        sm.sd_sid = "Y-" + sm.registry_id;
                    }
                    else
                    {
                        sm.sd_sid = sh.CreateMD5(sm.registry_id + sm.study_name + sm.enrolment_num);
                    }

                }

                page_study_list.Add(sm);
            }

            return page_study_list;
        }


        public Yoda_Record GetStudyDetails(ScrapingHelpers ch, YodaDataLayer repo, HtmlNode page, Summary sm, LoggingDataLayer logging_repo)
        {
            Yoda_Record st = new Yoda_Record(sm);
            List<SuppDoc> supp_docs = new List<SuppDoc>();

            // study properties
            var propsBlock = page.CssSelect("#block-views-trial-details-block-2");
            var leftcol = propsBlock.CssSelect(".left-col");
            var rightcol = propsBlock.CssSelect(".right-col");
            var props = leftcol.CssSelect(".views-field").Concat(rightcol.CssSelect(".views-field"));

            List<Identifier> study_identifiers = new List<Identifier>();
            List<Title> study_titles = new List<Title>();
            List<Reference> study_references = new List<Reference>();

            string label = "", value = "";
            foreach (HtmlNode fieldNode in props)
            {
                // get label
                var labelNode = fieldNode.CssSelect(".views-label").FirstOrDefault();
                if (labelNode != null)
                {
                    label = labelNode.InnerText.Trim();

                    value = HttpUtility.UrlDecode(fieldNode.InnerText) ?? "";
                    value = value?.Replace("\n", "")?.Replace("\r", "")?.Trim();
                    value = value?.Replace("&amp;", "&")?.Replace("&nbsp;", " ")?.Trim();
                    value = value?.Replace("&#039;", "'");
                    value = value.Substring(label.Length).Trim();

                    switch (label)
                    {
                        case "Generic Name": st.compound_generic_name = value; break;
                        case "Product Name": st.compound_product_name = value; break;
                        case "Therapeutic Area": st.therapaeutic_area = value; break;
                        case "Enrollment": st.enrolment = value; break;
                        case "% Female": st.percent_female = value; break;
                        case "% White": st.percent_white = value; break;
                        case "Product Class": st.product_class = value; break;
                        case "Sponsor Protocol Number": st.sponsor_protocol_id = value; break;
                        case "Data Partner": st.data_partner = value; break;
                        case "Condition Studied": st.conditions_studied = value; break;
                        default:
                            {
                                //logging_repo.LogLine(label);
                                break;
                            }
                    }
                }
            }


            // org = sponsor - from CGT / ISRCTN tables if registered, otherwise from pp table
            // In one case there is no sponsor id.
            // First obtain the data from the other sources...

            string reg_id = st.registry_id;
            SponsorDetails sponsor = null;
            StudyDetails sd = null;

            if (reg_id.StartsWith("NCT"))
            {
                // use nct_id to get sponsor id and name
                sponsor = repo.FetchSponsorFromNCT(reg_id);
                sd = repo.FetchStudyDetailsFromNCT(reg_id);
                study_identifiers.Add(new Identifier(reg_id, 11, "Trial Registry ID", 100120, "ClinicalTrials.gov"));
            }
            else if (reg_id.StartsWith("ISRCTN"))
            {
                sponsor = repo.FetchSponsorFromISRCTN(reg_id);
                sd = repo.FetchStudyDetailsFromISRCTN(reg_id);
                study_identifiers.Add(new Identifier(reg_id, 11, "Trial Registry ID", 100126, "ISRCTN"));
            }
            else
            {
                sponsor = repo.FetchSponsorDetailsFromTable(st.sd_sid);
                sd = repo.FetchStudyDetailsFromTable(st.sd_sid);
            }

            // Then insert the data if available
            // Otherwise add as a new record to be manually completed

            if (sponsor != null)
            {
                st.sponsor_id = sponsor.org_id;
                st.sponsor = sponsor.org_name;
            }
            else
            {
                st.sponsor_id = null;
                st.sponsor = null;
                logging_repo.LogError("No sponsor found for " + st.yoda_title + ", at " + st.remote_url);
            }

            string protid = "";
            if (sd != null)
            {
                st.name_base_title = sd.display_title ?? "";
                st.brief_description = sd.brief_description ?? "";
                st.study_type_id = sd.study_type_id ?? 0;
            }
            else
            {
                st.study_type_id = 0;
                logging_repo.LogError("No study details found for " + st.yoda_title + ", at " + st.remote_url);
                protid = st.sponsor_protocol_id.Replace("/", "-").Replace("\\", "-").Replace(" ", "");
                repo.AddNewRecord(st.sd_sid, st.yoda_title, protid);
            }

            // revise sd_sid using protocol id from table
            // if study not registered somewhere...
            if (st.is_yoda_only && sponsor != null)
            {
                  // study already in not_registered table
                  st.sd_sid = "Y-" + sponsor.prot_id;
            }
                

            // list of documents
            var docsBlock = page.CssSelect("#block-views-trial-details-block-1");
            var docs = docsBlock.CssSelect(".views-field");

            foreach (HtmlNode docType in docs)
            {
                string docName = docType.InnerText.Trim();
                if (docName != "")
                {
                    SuppDoc suppdoc = new SuppDoc(docName);
                    supp_docs.Add(suppdoc);
                }
            }


            //icons at top of page
            var iconsBlock = page.CssSelect("#block-views-trial-details-block-3");

            // csr summary
            var csrSummary = iconsBlock.CssSelect(".views-field-field-study-synopsis");
            var csrLinkNode = csrSummary.CssSelect("b a").FirstOrDefault();
            string csrLink = "", csrComment = "";

            if (csrLinkNode != null)
            {
                csrLink = csrLinkNode.Attributes["href"].Value;
            }

            var csrCommentNode = csrSummary.CssSelect("p").FirstOrDefault();
            if (csrCommentNode != null)
            {
                csrComment = csrCommentNode.InnerText.Trim();
            }

            if (csrLink != "" || csrComment != "")
            {
                // add a new supp doc record
                SuppDoc suppdoc = new SuppDoc("CSR Summary");
                suppdoc.url = csrLink;
                suppdoc.comment = csrComment;
                supp_docs.Add(suppdoc);

                // is this the same link as in the main table
                // ought to be but...
                if (suppdoc.url != sm.csr_link)
                {
                    string report = "mismatch in csr summary link - study id " + st.sd_sid;
                    report += "\nicon csr link = " + suppdoc.url;
                    report += "\ntable csr link = " + sm.csr_link + "\n\n";
                    logging_repo.LogLine(report);
                }
            }

            // primary citation
            var primCitation = iconsBlock.CssSelect(".views-field-field-primary-citation");
            var citationLink = primCitation.CssSelect("a").FirstOrDefault();
            if (citationLink != null)
            {
                st.primary_citation_link = citationLink.Attributes["href"].Value;
            }
            else
            {
                var citationCommentNode = primCitation.CssSelect("p").FirstOrDefault();
                if (citationCommentNode != null)
                {
                    st.primary_citation_link = citationCommentNode.InnerText.Trim();
                }
            }

            // data specifcation
            var dataSpec = iconsBlock.CssSelect(".views-field-field-data-specification-spreads");
            var dataLinkNode = dataSpec.CssSelect("b a").FirstOrDefault();
            string dataLink = "", dataComment = "";
            if (dataLinkNode != null)
            {
                dataLink = dataLinkNode.Attributes["href"].Value;
            }

            var dataCommentNode = dataSpec.CssSelect("p").FirstOrDefault();
            if (dataCommentNode != null)
            {
                dataComment = dataCommentNode.InnerText.Trim();
            }

            if (dataLink != "" || dataComment != "")
            {
                SuppDoc matchingSD = ch.FindSuppDoc(supp_docs, "Data Definition Specification");
                if (matchingSD != null)
                {
                    matchingSD.url = dataLink;
                    matchingSD.comment = dataComment;
                }
                else
                {
                    // add a new supp doc record
                    SuppDoc suppdoc = new SuppDoc("Data Definition Specification");
                    suppdoc.url = dataLink;
                    suppdoc.comment = dataComment;
                    supp_docs.Add(suppdoc);
                }
            }

            //annotated CRFs
            var annotCRF = iconsBlock.CssSelect(".views-field-field-annotated-crf");
            var crfLinkNode = annotCRF.CssSelect("b a").FirstOrDefault();
            string crfLink = "", crfComment = "";
            if (crfLinkNode != null)
            {
                crfLink = crfLinkNode.Attributes["href"].Value;
            }

            var crfCommentNode = annotCRF.CssSelect("p").FirstOrDefault();
            if (crfCommentNode != null)
            {
                crfComment = crfCommentNode.InnerText.Trim();
            }

            if (crfLink != "" || crfComment != "")
            {
                SuppDoc matchingSD = ch.FindSuppDoc(supp_docs, "Annotated Case Report Form");
                if (matchingSD != null)
                {
                    matchingSD.url = crfLink;
                    matchingSD.comment = crfComment;
                }
                else
                {
                    // add a new supp doc record
                    SuppDoc suppdoc = new SuppDoc("Annotated Case Report Form");
                    suppdoc.url = crfLink;
                    suppdoc.comment = crfComment;
                    supp_docs.Add(suppdoc);
                }
            }


            // eliminate supp_docs that are explicitly not available
            List<SuppDoc> supp_docs_available = new List<SuppDoc>();
            foreach (SuppDoc suppdoc in supp_docs)
            {
                // remove null comments
                suppdoc.comment = suppdoc.comment ?? "";
                bool add_this_doc = true;

                // if link present...
                if (suppdoc.url != null && suppdoc.url.Trim() != "")
                {
                    suppdoc.comment = "Available now";
                }

                if (suppdoc.comment != "")
                {
                    // exclude docs explicitly described as not available
                    if (suppdoc.comment.ToLower() == "not available" || suppdoc.comment.ToLower() == "not yet available"
                        || suppdoc.comment.ToLower() == "not yet avaiable")
                    {
                        add_this_doc = false;
                    }
                }
                else
                {
                    // default if no comment or link
                    suppdoc.comment = "Available upon data request approval";
                }

                if (add_this_doc) supp_docs_available.Add(suppdoc);
            }


            if (st.sponsor_protocol_id != "")
            {
                study_identifiers.Add(new Identifier(st.sponsor_protocol_id, 14, "Sponsor ID", sponsor?.org_id, sponsor?.org_name));
            }

            // for the study, add the yoda title (seems to be the full scientific title)

            study_titles.Add(new Title(st.sd_sid, st.yoda_title, 18, "Other scientific title", true, "From YODA web page"));

            // create study references (pmids)
            if (st.primary_citation_link.Contains("http"))
            {
                // extract pmid
                if (st.primary_citation_link.Contains("/pubmed/"))
                {
                    // drop this common suffix
                    string link = st.primary_citation_link.Replace("?dopt=Abstract", "");
                    int pubmed_pos = link.IndexOf("/pubmed/");
                    link = link.Substring(pubmed_pos + 8);
                    if (Int32.TryParse(link, out int pmid_as_int))
                    {
                        study_references.Add(new Reference(link));
                    }
                }

                else if (st.primary_citation_link.Contains("/pmc/articles/"))
                {
                    // need to interrogate NLM API 
                    int pmc_pos = st.primary_citation_link.IndexOf("/pmc/articles/");
                    string pmc_id = st.primary_citation_link.Substring(pmc_pos + 14);
                    pmc_id = pmc_id.Replace("/", "");
                    string pubmed_id = ch.GetPMIDFromNLM(pmc_id);
                    if (pubmed_id != null && pubmed_id != "")
                    {
                        study_references.Add(new Reference(pubmed_id));
                    }
                }

                else
                {
                    // else try and retrieve from linking out to the pubmed page
                    string pubmed_id = ch.GetPMIDFromPage(st.primary_citation_link);
                    if (pubmed_id != null && pubmed_id != "")
                    {
                        study_references.Add(new Reference(pubmed_id));
                    }
                }
            }

            // for all studies there is a data object which is the YODA page itself, 
            // as a web based study overview...
            st.supp_docs = supp_docs_available;
            st.study_identifiers = study_identifiers;
            st.study_titles = study_titles;
            st.study_references = study_references;
            return st;
        }
    }


    

}
