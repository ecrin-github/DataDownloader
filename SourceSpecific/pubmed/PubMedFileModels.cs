using System;
using System.Collections.Generic;

namespace DataDownloader.pubmed
{
	public class PubMedRecord
	{
		// largely a copy of a biolincc record - needs replacing
		// In fact the PubMed record basically transferred without analysis
		// simply split into separate articvles from a list of 10...

		// when operating off the back of a search the selection of the individual; ids, and id strings,m will be different
		// but otherwise the porocess remains the same


		public string sd_oid { get; set; }
		public string remote_url { get; set; }
		public string display_title { get; set; }
		public string public_title { get; set; }
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

		public PubMedRecord()
		{}
	}
	



	
}