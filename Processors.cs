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

namespace DataDownloader
{
	public class BioLINCC_Processor
	{
		HelperFunctions hp;
	
		
		public BioLINCC_Processor()
		{
			hp = new HelperFunctions();
		}


		public BioLINCC_Record GetStudyDetails(ScrapingBrowser browser, DataLayer repo, int study_id, HtmlNode row)
		{
			BioLINCC_Record st = new BioLINCC_Record(study_id);

			// get the basic information from the row
			// passed from the summary studies table
			HtmlNode[] cols = row.CssSelect("td").ToArray();

			// 4 columns in each row, first of which has a link
			// the second the acronym, the third the summarey of resources and 
			// the fourth the collection type
			string collection_type = cols[3].InnerText.Replace("\n", "").Replace("\r", "").Trim();
			// only consider biolincc managed resources
			if (collection_type == "Non-BioLINCC Resource") return null;

			HtmlNode link = cols[0].CssSelect("a").FirstOrDefault();
			st.display_title = link.InnerText.Trim();
			st.remote_url = "https://biolincc.nhlbi.nih.gov" + link.Attributes["href"].Value;
			st.acronym = cols[1].InnerText.Replace("\n", "").Replace("\r", "").Trim();
			st.resources_available = cols[2].InnerText.Replace("\n", "").Replace("\r", "").Trim();

			// set up 
			List<PrimaryDoc> primary_docs = new List<PrimaryDoc>();
			List<RegistryId> registry_ids = new List<RegistryId>();
			List<Resource> study_resources = new List<Resource>();
			List<AssocDoc> assoc_docs = new List<AssocDoc>();
			string nct_id = "";
			char[] splitter = { '(' };

			// Start the extraction for this study
			Console.WriteLine(study_id.ToString() + ":" + st.acronym);

			WebPage studyPage = browser.NavigateToPage(new Uri(st.remote_url));

			// main page components
			var main = studyPage.Find("div", By.Class("main"));
			var tables = main.CssSelect("div.study-box").ToArray();
			var table1 = tables[0];
			var table2 = tables[1];
			var sideBar = main.CssSelect("div.col-md-3");
			var descriptive_paras = studyPage.Find("div", By.Id("study-info")).FirstOrDefault();
			var description = descriptive_paras.CssSelect("div.col-sm-12").FirstOrDefault();

			// scan top table 
			HtmlNode[] entries = table1.CssSelect("p").ToArray();
			for (int i = 0; i < entries.Count(); i++)
			{
				HtmlNode entryBold = entries[i].CssSelect("b").FirstOrDefault();
				HtmlNode entrySupp = entries[i].CssSelect("em").FirstOrDefault();
				HtmlNode[] entryRefs = entries[i].CssSelect("a").ToArray();
				string attribute_name = entryBold.InnerText.Trim();
				string attribute_value = entries[i].InnerText;

				if (attribute_name == "Accession Number") st.sd_id = hp.CleanValue(attribute_value, attribute_name, entrySupp);
				if (attribute_name == "Study Type")
				{
					string study_type = hp.CleanValue(attribute_value, attribute_name, entrySupp);
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

				if (attribute_name == "Study Period") st.study_period = hp.CleanValue(attribute_value, attribute_name, entrySupp);

				if (attribute_name == "Date Prepared") st.date_prepared = hp.CleanValue(attribute_value, attribute_name, entrySupp);

				if (attribute_name == "Last Updated")
				{
					st.last_updated = hp.CleanValue(attribute_value, attribute_name, entrySupp);
					if (st.last_updated == "N/A" || string.IsNullOrEmpty(st.last_updated))
					{
						st.last_revised_date = null;
					}
					else
					{
						// date is in the forem of MMMM d, yyyy, and needs to be split accordingly
						string last_updated_string = st.last_updated.Replace(", ", "|").Replace(",", "|").Replace(" ", "|");
						string[] updated_parts = last_updated_string.Split("|");
						int month = hp.GetMonthAsInt(updated_parts[0]);
						if (month > 0
							&& Int32.TryParse(updated_parts[1], out int day)
							&& Int32.TryParse(updated_parts[2], out int year))
						{
							st.last_revised_date = new DateTime(year, month, day);
						}
					}
				}

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
									pubmed_id = hp.GetPMIDFromNLM(browser, pmc_id);
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
						string comm_use_restrictions = hp.CleanValue(attribute_value, attribute_name, entrySupp);
						if (comm_use_restrictions != null)
						{
							comm_use_data_restrics = (comm_use_restrictions.ToLower() == "yes") ? true : false; ;
						}
					}

					if (attribute_name == "Data Restrictions Based On Area Of Research")
					{
						string aor_use_restrictions = hp.CleanValue(attribute_value, attribute_name, entrySupp);
						if (aor_use_restrictions != null)
						{
							data_restrics_based_on_aor = (aor_use_restrictions.ToLower() == "yes") ? true : false;
						}
					}

					if (attribute_name == "Specific Consent Restrictions") specific_consent_restrics = hp.CleanValue(attribute_value, attribute_name, entrySupp);
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
				string headerText = headerLine.InnerText.Trim();
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
									if (object_type_details != null)
									{
										object_type_id = object_type_details.type_id;
										object_type = object_type_details.type_name;
									}
								}
							}

							study_resources.Add(new Resource(doc_name, object_type_id, object_type, doc_type_id, doc_type,
																 access_type_id, url, size, sizeUnits));
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
				WebPage pubsPage = browser.NavigateToPage(new Uri(pubURL));
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
					// set up publication record
					AssocDoc pubdets = new AssocDoc(Links[i].attribute);

					System.Threading.Thread.Sleep(600);
					WebPage pubsDetailsPage = browser.NavigateToPage(new Uri(Links[i].url));
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

			#endregion

			// add in the study properties
			st.primary_docs = primary_docs;
			st.registry_ids = registry_ids;
			st.resources = study_resources;
			st.assoc_docs = assoc_docs;

			return st;

		}

	}


	public class Yoda_Processor
	{
		HelperFunctions hp;


		public Yoda_Processor()
		{
			hp = new HelperFunctions();
		}


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


		public Yoda_Record GetStudyDetails(ScrapingBrowser browser, DataLayer repo, HtmlNode page, Summary sm)
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

			st.id = id;
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
				SuppDoc matchingSD = hp.FindSuppDoc(supp_docs, "Data Definition Specification");
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
				SuppDoc matchingSD = hp.FindSuppDoc(supp_docs, "Annotated Case Report Form");
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
			string sd_id = link_to_page.Substring(last_slash_pos + 1);
			st.sd_id = sd_id;

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
				SponsorDetails sponsor = repo.FetchYodaSponsorDetailsFromTable(sd_id);
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
			study_titles.Add(new Title(st.sd_id, st.title, 18, "Other scientific title", is_default_title, "From YODA web page"));

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
					string pubmed_id = hp.GetPMIDFromNLM(browser, pmc_id);
					if (pubmed_id != null && pubmed_id != "")
					{
						study_references.Add(new Reference(pubmed_id));
					}
				}

				else
				{
					// else try and retrieve from linking out to the pubmed page
					string pubmed_id = hp.GetPMIDFromPage(browser, st.primary_citation_link);
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


	public class HelperFunctions
	{
		public SuppDoc FindSuppDoc(List<SuppDoc> supp_docs, string name)
		{
			SuppDoc sd = null;
			foreach (SuppDoc s in supp_docs)
			{
				if (s.doc_name == name)
				{
					sd = s;
					break;
				}
			}
			return sd;
		}


		public string CleanValue(string inputText, string attribute, HtmlNode entrySupp)
		{
			// lose the bold and / or italic headings and return
			// the trimmed content, minus any new lines / carriage returns
			string attValue = inputText.Replace(attribute, "");
			if (entrySupp != null)
			{
				attValue = attValue.Replace(entrySupp.InnerText, "");
			}
			return attValue.Replace("\n", "").Replace("\r", "").Trim();
		}


		public int GetMonthAsInt(string month_name)
		{
			try
			{
				return (int)(Enum.Parse<MonthsFull>(month_name));
			}
			catch (ArgumentException)
			{
				return 0;
			}

		}	
		
		
		public string GetPMIDFromNLM(ScrapingBrowser browser, string pmc_id)
		{
			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				ReadCommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true
			};


			string base_url = "https://www.ncbi.nlm.nih.gov/pmc/utils/idconv/v1.0/";
			base_url += "?tool=ECRIN-MDR&email=steve@canhamis.eu&versions=no&ids=";
			string query_url = base_url + pmc_id + "&format=json";

			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(query_url);
			request.Method = "GET";
			WebResponse response = request.GetResponse();

			// assumes response is in utf-8
			MemoryStream ms = new MemoryStream();
			response.GetResponseStream().CopyTo(ms);
			byte[] response_data = ms.ToArray();

			PMCResponse PMC_object = JsonSerializer.Deserialize<PMCResponse>(response_data, options);
			return PMC_object?.records[0]?.pmid;
		}


		public string GetPMIDFromPage(ScrapingBrowser browser, string citation_link)
		{
			string pmid = "";
			// construct url
			var page = browser.NavigateToPage(new Uri(citation_link));
			// only works with pmid pages, that have this dl tag....
			HtmlNode ids_div = page.Find("dl", By.Class("rprtid")).FirstOrDefault();
			if (ids_div != null)
			{
				HtmlNode[] dts = ids_div.CssSelect("dt").ToArray();
				HtmlNode[] dds = ids_div.CssSelect("dd").ToArray();

				if (dts != null && dds != null)
				{
					for (int i = 0; i < dts.Length; i++)
					{
						string dts_type = dts[i].InnerText.Trim();
						if (dts_type == "PMID:")
						{
							pmid = dds[i].InnerText.Trim();
						}
					}
				}
			}
			return pmid;
		}


		public string CreateMD5(string input)
		{
			// Use input string to calculate MD5 hash
			using (MD5 md5 = MD5.Create())
			{
				byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
				byte[] hashBytes = md5.ComputeHash(inputBytes);

				// return as base64 string
				// 16 bytes = (5*4) characters + XX==, 
				// 24 rather than 32 hex characters
				return Convert.ToBase64String(hashBytes);

				/*
				// Convert the byte array to hexadecimal string
				StringBuilder sb = new StringBuilder();
				for (int i = 0; i < hashBytes.Length; i++)
				{
					sb.Append(hashBytes[i].ToString("X2"));
				}
				return sb.ToString();
				*/
			}
		}
	}

	public enum MonthsFull
	{
		January = 1, February, March, April, May, June,
		July, August, September, October, November, December
	};
}
