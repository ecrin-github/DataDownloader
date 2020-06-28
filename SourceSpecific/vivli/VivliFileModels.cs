using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;

namespace DataDownloader.vivli
{
	
	[Table("pp.studies")]
	public class VivliRecord
	{
		[ExplicitKey]
		public int id { get; set; }
		public string vivli_id { get; set; }
		public string study_title { get; set; }
		public string acronym { get; set; }
		public string pi_firstname { get; set; }
		public string pi_lastname { get; set; }
		public string pi_orcid_id { get; set; }
		public string protocol_id { get; set; }
		public string sponsor { get; set; }
		public string nct_id { get; set; }
		public string dobj_id { get; set; }
		public string data_prot_level { get; set; }
		public bool? save_ipd_for_future { get; set; }
		public string ipd_package_id { get; set; }
		public string metadata_package_id { get; set; }
		public string ipd_content_type { get; set; }
		public string study_metadata_doi { get; set; }
		public string study_req_behav { get; set; }
		public string bulk_upload_type { get; set; }
		public bool? downloadable_ipd_package { get; set; }
		public bool? ipd_data_supp_on_submission { get; set; }
		public string doi_stem { get; set; }
		public string ipd_package_doi { get; set; }
		public string additional_info { get; set; }
		public DateTime? date_created { get; set; }
		public DateTime? date_updated { get; set; }
	}

	[Table("pp.data_packages")]
	public class PackageRecord
	{
		[ExplicitKey] 
		public int id { get; set; }
		public string vivli_id { get; set; }
		public string vivli_study_id { get; set; }
		public string package_doi { get; set; }
		public string doi_stem { get; set; }
		public string package_title { get; set; }
		public string status { get; set; }
		public bool? downloadable { get; set; }
		public bool? files_dlable_before { get; set; }
		public string package_type { get; set; }
		public string sec_anal_dois { get; set; }
	}

	[Table("pp.data_objects")]
	public class ObjectRecord
	{
		public int id { get; set; }
		public int package_id { get; set; }
		public string object_type { get; set; }
		public string object_name { get; set; }
		public string comment { get; set; }
		public bool? is_complete { get; set; }
		public string size_kb { get; set; }
		public DateTime? updated { get; set; }
		public string package_doi { get; set; }
	}


	public class VivliURL
	{
		public int id { get; set; }
		public string name { get; set; }
		public string type { get; set; }		
		public string doi { get; set; }
		public string vivli_url { get; set; }
	}




}