using HtmlAgilityPack;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScrapySharp.Extensions;
using ScrapySharp.Html;
using ScrapySharp.Network;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using System.Security.Cryptography;

namespace DataDownloader.yoda
{
	public class Yoda_Processor
	{
		public List<Summary> GetStudyInitialDetails(WebPage homePage, int pagenum)
		{
			string cellValue = "";
			int n = 1000 + (pagenum * 10);
			var pageContent = homePage.Find("div", By.Class("view-content"));
			HtmlNode[] studyRows = pageContent.CssSelect("tbody tr").ToArray();
			List<Summary> page_study_list = new List<Summary>();

			foreach (HtmlNode row in studyRows)
			{
				n++;
				// 6 columns in each row, 
				// 0: NCT number, 1: generic name, 2: title, with link 3: Enrolment number, 
				// 4: CSR download link, 5: view field ops (?)

				Summary st = new Summary();
				st.id = n;
				HtmlNode[] cols = row.CssSelect("td").ToArray();
				for (int i = 0; i < 5; i++)
				{
					HtmlNode col = cols[i];
					cellValue = col.InnerText?.Replace("\n", "")?.Replace("\r", "")?.Trim() ?? "";
					if (cellValue != "") cellValue = cellValue.Replace("??", " ").Replace("&#039;", "'");
					switch (i)
					{
						case 0: st.nct_number = cellValue; break;
						case 1:
							{
								st.generic_name = cellValue;
								break;
							}
						case 2:
							{
								st.study_name = cellValue;
								HtmlNode link = col.CssSelect("a").FirstOrDefault();
								st.details_link = "https://yoda.yale.edu" + link.Attributes["href"].Value;
								break;
							}
						case 3:
							{
								if (cellValue != "") cellValue = cellValue.Replace(" ", "");
								st.enrolment_num = cellValue;
								break;
							}
						case 4:
							{
								if (cellValue != "")
								{
									HtmlNode link = col.CssSelect("a").FirstOrDefault();
									st.csr_link = link.Attributes["href"].Value;
								}
								else
								{
									st.csr_link = "";
								}
								break;
							}
					}
				}
				page_study_list.Add(st);
			}

			return page_study_list;
		}


		public Yoda_Record GetStudyDetails(ScrapingBrowser browser, YodaDataLayer repo, HtmlNode page, Summary sm)
		{
			Yoda_Record st = new Yoda_Record();
			List<SuppDoc> supp_docs = new List<SuppDoc>();

			int id = sm.id;
			string nct_number = sm.nct_number ?? "";

			// title
			var titleBlock = page.CssSelect("#block-views-trial-details-block-4").FirstOrDefault();
			string title = titleBlock.InnerText?.Replace("\n", "")?.Replace("\r", "")?.Trim();
			title = title.Replace("&#039;", "'");
			// is this the same title as in the main table
			// ought to be but...
			if (title != sm.study_name)
			{
				string report = "mismatch in study title - study id " + id.ToString();
				report += "\npage title = " + title;
				report += "\nstudy name = " + sm.study_name + "\n\n";
				Console.WriteLine(report);
			}

			st.nct_number = nct_number;
			st.is_yoda_only = (nct_number.StartsWith("NCT") || nct_number.StartsWith("ISRCTN")) ? false : true;
			st.title = title;
			st.remote_url = sm.details_link;

			Console.WriteLine(id.ToString());

			// study properties
			var propsBlock = page.CssSelect("#block-views-trial-details-block-2");
			var leftcol = propsBlock.CssSelect(".left-col");
			var rightcol = propsBlock.CssSelect(".right-col");
			var props = leftcol.CssSelect(".views-field").Concat(rightcol.CssSelect(".views-field"));

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
						case "% White": st.percent_male = value; break;
						case "Product Class": st.product_class = value; break;
						case "Sponsor Protocol Number": st.sponsor_protocol_id = value; break;
						case "Data Partner": st.data_partner = value; break;
						case "Condition Studied": st.conditions_studied = value; break;
						case "Mean/Median Age (Years)": st.mean_age = value; break;
						default:
							{
								Console.WriteLine(label);
								break;
							}
					}
				}
			}


			// list of documents
			var docsBlock = page.CssSelect("#block-views-trial-details-block-1");
			var docs = docsBlock.CssSelect(".views-field");

			foreach (HtmlNode docType in docs)
			{
				string docName = docType.InnerText.Trim();
				if (docName != "")
				{
					SuppDoc sd = new SuppDoc(docName);
					supp_docs.Add(sd);
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
				SuppDoc sd = new SuppDoc("CSR Summary");
				sd.url = csrLink;
				sd.comment = csrComment;
				supp_docs.Add(sd);

				// is this the same link as in the main table
				// ought to be but...
				if (sd.url != sm.csr_link)
				{
					string report = "mismatch in csr summary link - study id " + id.ToString();
					report += "\nicon csr link = " + sd.url;
					report += "\ntable csr link = " + sm.csr_link + "\n\n";
					Console.WriteLine(report);
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
				SuppDoc matchingSD = ScrapingHelpers.FindSuppDoc(supp_docs, "Data Definition Specification");
				if (matchingSD != null)
				{
					matchingSD.url = dataLink;
					matchingSD.comment = dataComment;
				}
				else
				{
					// add a new supp doc record
					SuppDoc sd = new SuppDoc("Data Definition Specification");
					sd.url = dataLink;
					sd.comment = dataComment;
					supp_docs.Add(sd);
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
				SuppDoc matchingSD = ScrapingHelpers.FindSuppDoc(supp_docs, "Annotated Case Report Form");
				if (matchingSD != null)
				{
					matchingSD.url = crfLink;
					matchingSD.comment = crfComment;
				}
				else
				{
					// add a new supp doc record
					SuppDoc sd = new SuppDoc("Annotated Case Report Form");
					sd.url = crfLink;
					sd.comment = crfComment;
					supp_docs.Add(sd);
				}
			}


			// eliminate supp_docs that are explicitly not available
			List<SuppDoc> supp_docs_available = new List<SuppDoc>();
			foreach (SuppDoc sd in supp_docs)
			{
				// remove null comments
				sd.comment = sd.comment ?? "";
				bool add_this_doc = true;

				// if link present...
				if (sd.url != null && sd.url.Trim() != "")
				{
					sd.comment = "Available now";
				}

				if (sd.comment != "")
				{
					// exclude docs explicitly described as not available
					if (sd.comment.ToLower() == "not available" || sd.comment.ToLower() == "not yet available"
						|| sd.comment.ToLower() == "not yet avaiable")
					{
						add_this_doc = false;
					}
				}
				else
				{
					// default if no comment or link
					sd.comment = "Available upon data request approval";
				}

				if (add_this_doc) supp_docs_available.Add(sd);
			}

			// create study attributes 
			// first obtain an sd_id as an MD5 hash of the title, plus protocol id
			// there is one instance of two studies with exactly the same title...
			// the url to the details page, however, must be unique...
			string link_to_page = sm.details_link;
			int last_slash_pos = link_to_page.LastIndexOf("/");
			string sd_sid = link_to_page.Substring(last_slash_pos + 1);
			st.sd_sid = sd_sid;

			List<Identifier> study_identifiers = new List<Identifier>();
			List<Title> study_titles = new List<Title>();
			List<Reference> study_references = new List<Reference>();

            int? sponsor_org_id;
			string sponsor_org;
			string identifier_value;

			if (nct_number.StartsWith("NCT"))
			{
                // use nct_id to get sponsor id and name
				SponsorDetails sponsor = repo.FetchYodaSponsorFromNCT(nct_number);
				sponsor_org_id = sponsor.org_id;
				sponsor_org = sponsor.org_name;
				identifier_value = nct_number;
				study_identifiers.Add(new Identifier(identifier_value, 11, "Trial Registry ID", 100120, "ClinicalTrials.gov"));
			}
			else if (nct_number.StartsWith("ISRCTN"))
			{
				SponsorDetails sponsor = repo.FetchYodaSponsorFromISRCTN(nct_number);
				sponsor_org_id = sponsor.org_id;
				sponsor_org = sponsor.org_name;
				identifier_value = nct_number;
				study_identifiers.Add(new Identifier(identifier_value, 11, "Trial Registry ID", 100126, "ISRCTN"));
			}
			else
			{
				SponsorDetails sponsor = repo.FetchYodaSponsorDetailsFromTable(sd_sid);
				if (sponsor == null)
				{
					sponsor_org_id = 0;
					sponsor_org = "";
					Console.WriteLine("No sponsor found for " + title);
				}
				else
				{
					sponsor_org_id = sponsor.org_id;
					sponsor_org = sponsor.org_name;
				}
			}

			st.sponsor = sponsor_org;
			st.sponsor_id = sponsor_org_id;

			// org = sponsor - from CGT / ISRCTN tables if registered, otherwise from pp table
			// In one case there is no sponsor id
			if (st.sponsor_protocol_id != "")
			{
				study_identifiers.Add(new Identifier(st.sponsor_protocol_id, 14, "Sponsor ID", sponsor_org_id, sponsor_org));
			}

			// for the study, add the title (seems to be the full scientific title)
			bool is_default_title = (st.is_yoda_only) ? true : false;
			study_titles.Add(new Title(st.sd_sid, st.title, 18, "Other scientific title", is_default_title, "From YODA web page"));

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
					string pubmed_id = ScrapingHelpers.GetPMIDFromNLM(browser, pmc_id);
					if (pubmed_id != null && pubmed_id != "")
					{
						study_references.Add(new Reference(pubmed_id));
					}
				}

				else
				{
					// else try and retrieve from linking out to the pubmed page
					string pubmed_id = ScrapingHelpers.GetPMIDFromPage(browser, st.primary_citation_link);
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
