using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DataDownloader.who
{
    public class WHO_Processor
	{
		public WHORecord ProcessStudyDetails(WHO_SourceRecord sr)
		{
			WHORecord r = new WHORecord();

			r.sd_sid = sr.TrialID;
			int source_id = get_reg_source(sr.TrialID);

			if (source_id == 100120 || source_id == 100123 || source_id == 100126)
			{ 
				// no need to process these - details input directly from registry
				// (for CGT, ISRCTN, EU CTR)
				return null; 
			}

			// otherwise proceed

			r.secondary_ids = new List<Secondary_Id>();
			r.study_features = new List<StudyFeature>();
			r.country_list = new List<string>();
			r.condition_list = new List<string>();

			r.source_id = source_id;
			r.record_date = DateHelpers.iso_date(sr.RecordDateString);

			r.secondary_ids.AddRange(split_ids(r.sd_sid, sr.SecondaryIDs, "secondary ids"));

			r.public_title = WHOHelpers.tidy_string(sr.public_title);
			r.scientific_title = WHOHelpers.tidy_string(sr.Scientific_title);
			r.remote_url = WHOHelpers.tidy_string(sr.url);

			r.public_contact_givenname = WHOHelpers.tidy_string(sr.Public_Contact_Firstname);
			r.public_contact_familyname = WHOHelpers.tidy_string(sr.Public_Contact_Lastname);
			r.public_contact_affiliation = WHOHelpers.tidy_string(sr.Public_Contact_Affiliation);
			r.public_contact_email = WHOHelpers.tidy_string(sr.Public_Contact_Email);
     		r.scientific_contact_givenname = WHOHelpers.tidy_string(sr.Scientific_Contact_Firstname);
			r.scientific_contact_familyname = WHOHelpers.tidy_string(sr.Scientific_Contact_Lastname);
			r.scientific_contact_affiliation = WHOHelpers.tidy_string(sr.Scientific_Contact_Affiliation);
			r.scientific_contact_email = WHOHelpers.tidy_string(sr.Scientific_Contact_Email);

			string study_type = WHOHelpers.tidy_string(sr.study_type);
			if (study_type != null)
			{
				string stype = study_type.ToLower();
				if (stype.StartsWith("intervention"))
				{
					r.study_type = "Interventional";
				}
				else if (stype.StartsWith("observation")
					  || stype.StartsWith("epidem"))
				{
					r.study_type = "Observational";
				}
				else
				{
					r.study_type = "Other (" + r.study_type + ")";
				}
			}
			
			string study_status = WHOHelpers.tidy_string(sr.Recruitment_status);
			if (study_status != null && study_status.Length > 5)
            {
				string status = study_status.ToLower();
				if (status == "complete"
					  || status == "completed"
                      || status == "complete: follow-up complete"
					  || status == "complete: follow up complete"
					  || status == "data analysis completed"
					  || status == "main results already published")
				{
					r.study_status = "Completed";
				}
				else if (status == "complete: follow-up continuing"
					  || status == "complete: follow up continuing"
					  || status == "active, not recruiting"
					  || status == "closed to recruitment of participants"
					  || status == "no longer recruiting"
					  || status == "not recruiting"
					  || status == "recruitment completed")
				{
					r.study_status = "Active, not recruiting";
				}
				else if (status == "recruiting"
					  || status == "open public recruiting"
					  || status == "open to recruitment")
				{
					r.study_status = "Recruiting";
				}
				else if (status.Contains("pending")
					  || status == "not yet recruiting")
				{
					r.study_status = "Not yet recruiting";
				}
				else if (status.Contains("suspended")
					  || status.Contains("temporarily closed"))
				{
					r.study_status = "Suspended";
				}
				else if (status.Contains("terminated")
					  || status.Contains("stopped early"))
				{
					r.study_status = "Terminated";
				}
				else if (status.Contains("withdrawn"))
				{
					r.study_status = "Withdrawn";
				}
				else if (status.Contains("enrolling by invitation"))
				{
					r.study_status = "Enrolling by invitation";
				}
				else 
				{
					r.study_status = "Other (" + study_status + ")";
				}

			}

			r.date_registration = DateHelpers.iso_date(sr.Date_registration);
			r.date_enrollement = DateHelpers.iso_date(sr.Date_enrollement);

			r.target_size = WHOHelpers.tidy_string(sr.Target_size);
			r.primary_sponsor = WHOHelpers.tidy_string(sr.Primary_sponsor);
			r.secondary_sponsors = WHOHelpers.tidy_string(sr.Secondary_sponsors);
			r.source_support = WHOHelpers.tidy_string(sr.Source_Support);

			string design_list = WHOHelpers.tidy_string(sr.study_design);
			if (design_list != null)
            {
				bool nonrandomised = false;
				string design = design_list.ToLower();
				if (design.Contains("non-randomized")
				 || design.Contains("nonrandomized")
				 || design.Contains("non-randomised")
				 || design.Contains("nonrandomised"))
				{
					r.study_features.Add(new StudyFeature(22, "Allocation type", 210, "Nonrandomised"));
					nonrandomised = true;
				}

				if (!nonrandomised && 
					 (  design.Contains("randomized")
					 || design.Contains("randomised")))
				{
					r.study_features.Add(new StudyFeature(22, "Allocation type", 205, "Randomised"));
				}

				if (design.Contains("parallel"))
				{
					r.study_features.Add(new StudyFeature(23, "Intervention model", 305, "Parallel assignment"));
				}

				if (design.Contains("crossover"))
				{
					r.study_features.Add(new StudyFeature(23, "Intervention model", 310, "Crossover assignment"));
				}

				if (design.Contains("factorial"))
				{
					r.study_features.Add(new StudyFeature(23, "Intervention model", 315, "Factorial assignment"));
				}

				if (design.Contains("open label")
			     || design.Contains("open-label")
				 || design.Contains("no mask")
				 || design.Contains("masking not used"))
				{
					r.study_features.Add(new StudyFeature(24, "Masking", 500, "None (Open Label)"));
				}
				else if (design.Contains("single blind")
				 || design.Contains("single-blind"))
				{
					r.study_features.Add(new StudyFeature(24, "Masking", 505, "Single"));
				}
				else if (design.Contains("double blind")
				 || design.Contains("double-blind"))
				{
					r.study_features.Add(new StudyFeature(24, "Masking", 510, "Double"));
				}
				else if (design.Contains("triple blind")
				 || design.Contains("triple-blind"))
				{
					r.study_features.Add(new StudyFeature(24, "Masking", 515, "Triple"));
				}
				else if (design.Contains("quadruple blind")
				 || design.Contains("quadruple-blind"))
				{
					r.study_features.Add(new StudyFeature(24, "Masking", 520, "Quadruple"));
				}
				else if (design.Contains("blind")
				 || design.Contains("mask"))
				{
					r.study_features.Add(new StudyFeature(24, "Masking", 5000, design_list));
				}
			}

			string phase_list = WHOHelpers.tidy_string(sr.phase);
			if (phase_list != null)
			{
				string phase = phase_list.ToLower();
				if (phase != "not selected"	&& phase != "not applicable" && phase != "n/a")
				{
					if (phase == "phase 0"
					 || phase == "phase-0"
					 || phase == "phase0"
					 || phase == "0"
					 || phase == "0 (exploratory trials)"
					 || phase == "phase 0 (exploratory trials)"
					 || phase == "0 (exploratory trials)")
					{
						r.study_features.Add(new StudyFeature(20, "Phase", 105, "Early phase 1"));
					}
					else if (phase == "1"
						  || phase == "i"
						  || phase == "i (phase i study)"
						  || phase == "phase-1"
						  || phase == "phase 1"
						  || phase == "phase i"
						  || phase == "phase1")
					{
						r.study_features.Add(new StudyFeature(20, "phase", 110, "Phase 1"));
					}
					else if (phase == "1-2"
						  || phase == "1 to 2"
						  || phase == "i-ii"
						  || phase == "i+ii (phase i+phase ii)"
						  || phase == "phase 1-2"
						  || phase == "phase 1 / phase 2"
						  || phase == "phase 1/ phase 2"
						  || phase == "phase 1/phase 2"
						  || phase == "phase i,ii"
						  || phase == "phase1/phase2")
					{
						r.study_features.Add(new StudyFeature(20, "Phase", 115, "Phase 1/Phase 2"));
					}
					else if (phase == "2"
						  || phase == "2a"
						  || phase == "2b"
						   || phase == "ii"
						  || phase == "ii (phase ii study)"
						  || phase == "iia"
						  || phase == "iib"
						  || phase == "phase-2"
						  || phase == "phase 2"
						  || phase == "phase ii"
						  || phase == "phase2")
					{
						r.study_features.Add(new StudyFeature(20, "Phase", 120, "Phase 2"));
					}
					else if (phase == "2-3"
						 || phase == "ii-iii"
						 || phase == "phase 2-3"
						 || phase == "phase 2 / phase 3"
						 || phase == "phase 2/ phase 3"
						 || phase == "phase 2/phase 3"
						 || phase == "phase2/phase3"
						 || phase == "phase ii,iii")
					{
						r.study_features.Add(new StudyFeature(20, "Phase", 125, "Phase 2/Phase 3"));
					}
					else if (phase == "3"
						  || phase == "iii"
						  || phase == "iii (phase iii study)"
						  || phase == "iiia"
						  || phase == "iiib"
						  || phase == "3-4"
						  || phase == "phase-3"
						  || phase == "phase 3"
						  || phase == "phase 3 / phase 4"
						  || phase == "phase 3/ phase 4"
						  || phase == "phase3"
						  || phase == "phase iii")
					{
						r.study_features.Add(new StudyFeature(20, "Phase", 130, "Phase 3"));
					}
					else if (phase == "4"
						   || phase == "iv"
						   || phase == "iv (phase iv study)"
						   || phase == "phase-4"
						   || phase == "phase 4"
						   || phase == "post-market"
						   || phase == "post marketing surveillance"
						   || phase == "phase4"
						   || phase == "phase iv")
					{
						r.study_features.Add(new StudyFeature(20, "Phase", 135, "Phase 4"));
					}
					else
					{
						r.study_features.Add(new StudyFeature(20, "Phase", 1500, phase_list));
					}
				}
			}

			r.country_list = WHOHelpers.split_and_dedup_string(sr.Countries);

			r.condition_list = WHOHelpers.split_string(sr.Conditions);

			r.interventions = WHOHelpers.tidy_string(sr.Interventions);

			string agemin = WHOHelpers.tidy_string(sr.Agemin);
			if(agemin != null)
            {
				if (Regex.Match(agemin, @"\d+").Success)
                {
					r.agemin = Regex.Match(agemin, @"\d+").Value;
					r.agemin_units = DateHelpers.GetTimeUnits(agemin);
				}
            }

			string agemax = WHOHelpers.tidy_string(sr.Agemax);
			if (agemax != null)
			{
				if (Regex.Match(agemax, @"\d+").Success)
				{
					r.agemax = Regex.Match(agemax, @"\d+").Value;
					r.agemax_units = DateHelpers.GetTimeUnits(agemax);
				}
			}

			string gender = WHOHelpers.tidy_string(sr.Gender);
			if (gender != null)
			{
				string gen = gender.ToLower();
				if (gen.Contains("both"))
				{
					r.gender = "Both";
				}
				else
                {
					bool M, F = false;
					string gender_string = "";
					F = gen.Contains("female") || gen.Contains("women");
					string gen2 = F ? gen.Replace("female", "").Replace("women", "") : gen;
					M = gen2.Contains("male") || gen.Contains("men");
					if (M && F)
                    {
						gender_string = "Both";
					}
					else
                    {
						if (M) gender_string = "Male";
						if (F) gender_string = "Female";
					}
					if (gender_string == "")
                    {
						gender_string = "?? Unavle to classify (" + gender + ")";
					}
					r.gender = gender_string;
				}
			}
			else
			{
				r.gender = "Not provided";
			}

			r.inclusion_criteria = WHOHelpers.tidy_string(sr.Inclusion_Criteria);
			r.exclusion_criteria = WHOHelpers.tidy_string(sr.Exclusion_Criteria);
			r.primary_outcome = WHOHelpers.tidy_string(sr.Primary_Outcome);
			r.secondary_outcomes = WHOHelpers.tidy_string(sr.Secondary_Outcomes);

			r.bridging_flag = WHOHelpers.tidy_string(sr.Bridging_flag);
			if (r.bridging_flag != null && r.bridging_flag != r.sd_sid)
            {
				r.secondary_ids.Add(new Secondary_Id(r.sd_sid, r.bridging_flag, "bridging flag"));
    		}

			r.bridged_type = WHOHelpers.tidy_string(sr.Bridged_type);

			r.childs = WHOHelpers.tidy_string(sr.Childs);
			r.secondary_ids.AddRange(split_ids(r.sd_sid, sr.Childs, "bridged child recs"));

			r.type_enrolment = WHOHelpers.tidy_string(sr.type_enrolment);
			r.retrospective_flag = WHOHelpers.tidy_string(sr.Retrospective_flag);

			r.results_yes_no = WHOHelpers.tidy_string(sr.results_yes_no);
			r.results_actual_enrollment = WHOHelpers.tidy_string(sr.results_actual_enrollment);
			r.results_url_link = WHOHelpers.tidy_string(sr.results_url_link);
			r.results_summary = WHOHelpers.tidy_string(sr.results_summary);
			r.results_date_posted = DateHelpers.iso_date(sr.results_date_posted);
			r.results_date_first_publication = DateHelpers.iso_date(sr.results_date_first_publication);
			r.results_url_protocol = WHOHelpers.tidy_string(sr.results_url_protocol);
			r.results_date_completed = DateHelpers.iso_date(sr.results_date_completed);

			string ipd_plan = WHOHelpers.tidy_string(sr.results_IPD_plan);

			if (ipd_plan != null && ipd_plan.Length > 10)
            {
				if (ipd_plan.ToLower() != "not available" && ipd_plan.ToLower() != "not avavilable"
				&& ipd_plan.ToLower() != "not applicable" && !ipd_plan.ToLower().StartsWith("justification or reason for"))
                {
					r.ipd_plan = ipd_plan;
                }
			}

			string ipd_description = WHOHelpers.tidy_string(sr.results_IPD_description);
			if (ipd_description != null && ipd_description.Length > 10)
			{
				if (ipd_description.ToLower() != "not available" && ipd_description.ToLower() != "not avavilable"
				&& ipd_description.ToLower() != "not applicable" && !ipd_description.ToLower().StartsWith("justification or reason for"))
				{
					r.ipd_description = ipd_description;
				}
			}

			return r;
		}


		private List<Secondary_Id> split_ids(string sd_sid, string instring, string source_field)
		{
			List<Secondary_Id> sec_ids = new List<Secondary_Id>();
			if (!string.IsNullOrEmpty(instring))
			{
				string id_list = WHOHelpers.tidy_string(instring);
				if (!string.IsNullOrEmpty(id_list))
				{
					List<string> ids = id_list.Split(";").ToList();
					foreach (string s in ids)
					{
						char[] chars_to_lose = { ' ', '\'', '‘', '’', ';'};
						string s1 = s.Trim(chars_to_lose);
						if (s1.Length >= 4 && s1 != sd_sid)
						{
							string s2 = s1.ToLower();
							if (!(s2.StartsWith("none"))
								&& !(s2.StartsWith("nil"))
								&& !(s2.StartsWith("date"))
								&& !(s2.StartsWith("version"))
								&& !(s2.StartsWith("??")))
							sec_ids.Add(new Secondary_Id(sd_sid, s1, source_field));
						}
					}
				}
			}
			return sec_ids;
		}

		private int get_reg_source(string trial_id)
        {
			int source_id = 0;
			string tid = trial_id.ToUpper();
			if (tid.StartsWith("NCT"))
			{
				source_id = 100120;
			}
			else if (tid.StartsWith("EUCTR"))
            {
				source_id = 100123;
			}
			else if (tid.StartsWith("JPRN"))
			{
				source_id = 100127;
			}
			else if (tid.StartsWith("ACTRN"))
			{
				source_id = 100116;
			}
			else if (tid.StartsWith("RBR"))
			{
				source_id = 100117;
			}
			else if (tid.StartsWith("ChiCTR"))
			{
				source_id = 100118;
			}
			else if (tid.StartsWith("KCT"))
			{
				source_id = 100119;
			}
			else if (tid.StartsWith("CTRI"))
			{
				source_id = 100121;
			}
			else if (tid.StartsWith("RPCEC"))
			{
				source_id = 100122;
			}
			else if (tid.StartsWith("DRKS"))
			{
				source_id = 100124;
			}
			else if (tid.StartsWith("IRCT"))
			{
				source_id = 100125;
			}
			else if (tid.StartsWith("ISRCTN"))
			{
				source_id = 100126;
			}
			else if (tid.StartsWith("PACTR"))
			{
				source_id = 100128;
			}
			else if (tid.StartsWith("PER"))
			{
				source_id = 100129;
			}
			else if (tid.StartsWith("SLCTR"))
			{
				source_id = 100130;
			}
			else if (tid.StartsWith("TCTR"))
			{
				source_id = 100131;
			}
			
			else if (tid.StartsWith("NL") || tid.StartsWith("NTR"))
			{
				source_id = 100132;
			}
			else if (tid.StartsWith("LBCTR"))
			{
				source_id = 101989;
			}

			return source_id;
		}


	}
}



