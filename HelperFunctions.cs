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
