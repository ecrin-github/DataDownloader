using System;
using System.Collections.Generic;

namespace DataDownloader.biolincc
{
	public class BioLinccRecord
	{
		public string sd_sid { get; set; }
		public string remote_url { get; set; }
		public string title { get; set; }
		public string acronym { get; set; }
		public int? study_type_id { get; set; }
		public string study_type { get; set; }
		public string brief_description { get; set; }
		public string study_period { get; set; }
		public string date_prepared { get; set; }
		public DateTime? page_prepared_date { get; set; }
		public string last_updated { get; set; }
		public DateTime? last_revised_date { get; set; }
		public int publication_year { get; set; }
		public string study_website { get; set; }
		public int num_clinical_trial_urls { get; set; }
		public int num_primary_pub_urls { get; set; }
		public int num_associated_papers { get; set; }
		public string resources_available { get; set; }
		public int dataset_consent_type_id { get; set; }
		public string dataset_consent_type { get; set; }
		public string dataset_consent_restrictions { get; set; }

		public List<PrimaryDoc> primary_docs { get; set; }
		public List<RegistryId> registry_ids { get; set; }
		public List<Resource> resources { get; set; }
		public List<AssocDoc> assoc_docs { get; set; }
	}
	
	public class Link
	{
		public string attribute { get; set; }
		public string url { get; set; }

		public Link(string _attribute, string _url)
		{
			attribute = _attribute;
			url = _url;
		}
	}
	
	public class DataRestrictDetails
	{
		public int? org_id { get; set; }
		public string org_name { get; set; }
	}

	public class ObjectTypeDetails
	{
		public int? type_id { get; set; }
		public string type_name { get; set; }
	}


	public class RegistryId
	{
		public string url { get; set; }
		public string nct_id { get; set; }
		public string comment { get; set; }

		public RegistryId(string _url,	string _nctid, string _comment)
		{
			url = _url;
			nct_id = _nctid;
			comment = _comment;
		}

		public RegistryId()
		{}
	}

	public class Resource
	{
		public string doc_name { get; set; }
		public int? object_type_id { get; set; }
		public string object_type { get; set; }
		public int? doc_type_id { get; set; }
		public string doc_type { get; set; }
		public int? access_type_id { get; set; }
		public string url { get; set; }
		public string size { get; set; }
		public string size_units { get; set; }

		public Resource(string _doc_name, int? _object_type_id, string _object_type, int? _doc_type_id, 
			            string _doc_type, int? _access_type_id, string _url, string _size, string _size_units)
		{
			doc_name = _doc_name;
			object_type_id = _object_type_id;
			object_type = _object_type;
			doc_type_id = _doc_type_id;
			doc_type = _doc_type;
			access_type_id = _access_type_id;
			url = _url;
			size = _size;
			size_units = _size_units;
		}

		public Resource()
		{}
	}

	public class PrimaryDoc
	{
		public string url { get; set; }
		public string pubmed_id { get; set; }
		public string comment { get; set; }

		public PrimaryDoc(string _url,	string _pubmed_id, string _comment)
		{
			url = _url;
			pubmed_id = _pubmed_id;
			comment = _comment;
		}

		public PrimaryDoc()
		{}
	}

	public class AssocDoc
	{
		public string link_id { get; set; }
		public string pubmed_id { get; set; }
		public string pmc_id { get; set; }
		public string title { get; set; }
		public string display_title { get; set; }
		public string journal { get; set; }
		public string pub_date { get; set; }

		public AssocDoc(string _link_id)
		{
			link_id = _link_id;
		}

		public AssocDoc()
		{}
	}
}