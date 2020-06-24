using System;
using System.Collections.Generic;
using CsvHelper.Configuration.Attributes;
using Dapper.Contrib.Extensions;


namespace DataDownloader.who
{
	public class WHO_SourceRecord
	{
		[Index(0)] 
		public string TrialID { get; set; }
		[Index(1)]
		public string RecordDateString { get; set; }
		[Index(2)]
		public string SecondaryIDs { get; set; }
		[Index(3)]
		public string public_title { get; set; }
		[Index(4)]
		public string Scientific_title { get; set; }
		[Index(5)]
		public string url { get; set; }
		[Index(6)]
		public string Public_Contact_Firstname { get; set; }
		[Index(7)]
		public string Public_Contact_Lastname { get; set; }
		[Index(8)]
		public string Public_Contact_Address { get; set; }
		[Index(9)]
		public string Public_Contact_Email { get; set; }
		[Index(10)]
		public string Public_Contact_Tel { get; set; }
		[Index(11)]
		public string Public_Contact_Affiliation { get; set; }
		[Index(12)]
		public string Scientific_Contact_Firstname { get; set; }
		[Index(13)]
		public string Scientific_Contact_Lastname { get; set; }
		[Index(14)]
		public string Scientific_Contact_Address { get; set; }
		[Index(15)]
		public string Scientific_Contact_Email { get; set; }
		[Index(16)]
		public string Scientific_Contact_Tel { get; set; }
		[Index(17)]
		public string Scientific_Contact_Affiliation { get; set; }
		[Index(18)]
		public string study_type { get; set; }
		[Index(19)]
		public string study_design { get; set; }
		[Index(20)]
		public string phase { get; set; }
		[Index(21)]
		public string Date_registration { get; set; }
		[Index(22)]
		public string Date_enrollement { get; set; }
		[Index(23)]
		public string Target_size { get; set; }
		[Index(24)]
		public string Recruitment_status { get; set; }
		[Index(25)]
		public string Primary_sponsor { get; set; }
		[Index(26)]
		public string Secondary_sponsors { get; set; }
		[Index(27)]
		public string Source_Support { get; set; }
		[Index(28)]
		public string Countries { get; set; }
		[Index(29)]
		public string Conditions { get; set; }
		[Index(30)]
		public string Interventions { get; set; }
		[Index(31)]
		public string Agemin { get; set; }
		[Index(32)]
		public string Agemax { get; set; }
		[Index(33)]
		public string Gender { get; set; }
		[Index(34)]
		public string Inclusion_Criteria { get; set; }
		[Index(35)]
		public string Exclusion_Criteria { get; set; }
		[Index(36)]
		public string Primary_Outcome { get; set; }
		[Index(37)]
		public string Secondary_Outcomes { get; set; }
		[Index(38)]
		public string Bridging_flag { get; set; }
		[Index(39)]
		public string Bridged_type { get; set; }
		[Index(40)]
		public string Childs { get; set; }
		[Index(41)]
		public string type_enrolment { get; set; }
		[Index(42)]
		public string Retrospective_flag { get; set; }
		[Index(43)]
		public string results_actual_enrollment { get; set; }
		[Index(44)]
		public string results_url_link { get; set; }
		[Index(45)]
		public string results_summary { get; set; }
		[Index(46)]
		public string results_date_posted { get; set; }
		[Index(47)]
		public string results_date_first_publication { get; set; }
		[Index(48)]
		public string results_baseline_char { get; set; }
		[Index(49)]
		public string results_participant_flow { get; set; }
		[Index(50)]
		public string results_adverse_events { get; set; }
		[Index(51)]
		public string results_outcome_measures { get; set; }
		[Index(52)]
		public string results_url_protocol { get; set; }
		[Index(53)]
		public string results_IPD_plan { get; set; }
		[Index(54)]
		public string results_IPD_description { get; set; }
		[Index(55)]
		public string results_date_completed { get; set; }
		[Index(56)]
		public string results_yes_no { get; set; }
		[Index(57)]
		public string Ethics_Status { get; set; }
		[Index(58)]
		public string Ethics_Approval_Date { get; set; }
		[Index(59)]
		public string Ethics_Contact_Name { get; set; }
		[Index(60)]
		public string Ethics_Contact_Address { get; set; }
		[Index(61)]
		public string Ethics_Contact_Phone { get; set; }
		[Index(62)]
		public string Ethics_Contact_Email { get; set; }

	}

	public class WHORecord
	{
		public int source_id { get; set; }
		public string record_date { get; set; }
		public string sd_sid { get; set; }
		public string public_title { get; set; }
		public string scientific_title { get; set; }
		public string remote_url { get; set; }
		public string public_contact_givenname { get; set; }
		public string public_contact_familyname { get; set; }
		public string public_contact_email { get; set; }
		public string public_contact_affiliation { get; set; }
		public string scientific_contact_givenname { get; set; }
		public string scientific_contact_familyname { get; set; }
		public string scientific_contact_email { get; set; }
		public string scientific_contact_affiliation { get; set; }
		public string study_type { get; set; }
		public string date_registration { get; set; }
		public string date_enrollement { get; set; }
		public string target_size { get; set; }
		public string study_status { get; set; }
		public string primary_sponsor { get; set; }
		public string secondary_sponsors { get; set; }
		public string source_support { get; set; }
		public string interventions { get; set; }
		public string agemin { get; set; }
		public string agemin_units { get; set; }
		public string agemax { get; set; }
		public string agemax_units { get; set; }
		public string gender { get; set; }
		public string inclusion_criteria { get; set; }
		public string exclusion_criteria { get; set; }
		public string primary_outcome { get; set; }
		public string secondary_outcomes { get; set; }
		public string bridging_flag { get; set; }
		public string bridged_type { get; set; }
		public string childs { get; set; }
		public string type_enrolment { get; set; }
		public string retrospective_flag { get; set; }
		public string results_actual_enrollment { get; set; }
		public string results_url_link { get; set; }
		public string results_summary { get; set; }
		public string results_date_posted { get; set; }
		public string results_date_first_publication { get; set; }
		public string results_url_protocol { get; set; }
		public string ipd_plan { get; set; }
		public string ipd_description { get; set; }
		public string results_date_completed { get; set; }
		public string results_yes_no { get; set; }

		public List<Secondary_Id> secondary_ids { get; set; }
		public List<StudyFeature> study_features { get; set; }
		public List<string> country_list { get; set; }
		public List<string> condition_list { get; set; }
		public List<string> phase_list { get; set; }
	}


	public class Secondary_Id
	{
		public int source_a_id { get; set; }
		public string sd_a_sid { get; set; }
		public int source_b_id { get; set; }
		public string sd_b_sid { get; set; }
		public string source_field { get; set; }

		public Secondary_Id(string _sd_a_sid, string _sd_b_sid, 
			                string _source_field)
        {
			sd_a_sid = _sd_a_sid;
			sd_b_sid = _sd_b_sid;
			source_field = _source_field;
		}
	}


	public class StudyFeature
	{
		public int ftype_id { get; set; }
		public string ftype { get; set; }
		public int fvalue_id { get; set; }
		public string fvalue { get; set; }

		public StudyFeature(int _ftype_id, string _ftype,
			                int _fvalue_id, string _fvalue)
		{
			ftype_id = _ftype_id;
			ftype = _ftype;
			fvalue_id = _fvalue_id;
			fvalue = _fvalue;
		}
	}

}
