using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataDownloader.biolincc
{

    public class BioLinccDataLayer
    {
        private string _biolincc_connString;
        private string _ctg_connString;

        /// <summary>
        /// Parameterless constructor is used to automatically build
        /// the connection string, using an appsettings.json file that 
        /// has the relevant credentials (but which is not stored in GitHub).
        /// The json file also includes the root folder path, which is
        /// stored in the class's folder_base property.
        /// </summary>
        public BioLinccDataLayer()
        {
            IConfigurationRoot settings = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder();
            builder.Host = settings["host"];
            builder.Username = settings["user"];
            builder.Password = settings["password"];

            builder.Database = "biolincc";
            _biolincc_connString = builder.ConnectionString;

            builder.Database = "ctg";
            _ctg_connString = builder.ConnectionString;

        }


        public void RecreateMultiHBLIsTable()
        {
            using (var conn = new NpgsqlConnection(_biolincc_connString))
            {
                string sql_string = @"DROP TABLE IF EXISTS pp.biolincc_nct_links;
                CREATE TABLE pp.biolincc_nct_links (
                    sd_sid VARCHAR,
                    nct_id VARCHAR,
                    multi_biolincc_to_nct BOOL default false);";
                conn.Execute(sql_string);
            }
        }


        public void StoreLinks(string sd_sid, List<RegistryId> registry_ids)
        {
            using (var conn = new NpgsqlConnection(_biolincc_connString))
            {
                foreach (RegistryId id in registry_ids)
                {
                    string sql_string = @"Insert into pp.biolincc_nct_links(sd_sid, nct_id)
                        values('" + sd_sid + "', '" + id.nct_id + "');";
                    conn.Execute(sql_string);
                }
            }
        }


        public void UpdateLinkStatus()
        {
            using (var conn = new NpgsqlConnection(_biolincc_connString))
            {
                string sql_string = @"Update pp.biolincc_nct_links k
                set multi_biolincc_to_nct = true
                from 
                    (select nct_id
                     from pp.biolincc_nct_links
                     group by nct_id
                     having count(sd_sid) > 1) mults
                where k.nct_id = mults.nct_id;";
                conn.Execute(sql_string);
            }
        } 


        public bool GetMultiLinkStatus(string sd_sid)
        {
            using (var conn = new NpgsqlConnection(_biolincc_connString))
            {
                string sql_string = @"select multi_biolincc_to_nct
                from pp.biolincc_nct_links
                where sd_sid = '" + sd_sid.ToString() + "'";
                return conn.Query<bool>(sql_string).FirstOrDefault();
            }
        }


        public ObjectTypeDetails FetchDocTypeDetails(string doc_name)
        {
            using (var conn = new NpgsqlConnection(_biolincc_connString))
            {
                string sql_string = "Select type_id, type_name from pp.document_types ";
                sql_string += "where resource_name = '" + doc_name + "';";
                ObjectTypeDetails res =  conn.QueryFirstOrDefault<ObjectTypeDetails>(sql_string);
                if (res == null)
                {
                    // store the details in the table for later matching
                    sql_string = "Insert into pp.document_types (resource_name, type_id, type_name)";
                    sql_string += "values('" + doc_name + "', 0, 'to be added to lookup');";
                }
                return res;
            }
        }

        public SponsorDetails FetchSponsorFromNCT(string nct_id)
        {
            using (var conn = new NpgsqlConnection(_ctg_connString))
            {
                string sql_string = "Select organisation_id as org_id, organisation_name as org_name from ad.study_contributors ";
                sql_string += "where sd_sid = '" + nct_id + "' and contrib_type_id = 54;";
                return conn.QueryFirstOrDefault<SponsorDetails>(sql_string);
            }
        }


        public string FetchNameBaseFromNCT(string sd_sid)
        {
            using (var conn = new NpgsqlConnection(_ctg_connString))
            {
                string sql_string = "Select display_title from ad.studies ";
                sql_string += "where sd_sid = '" + sd_sid + "'";
                return conn.QueryFirstOrDefault<string>(sql_string);
            }
        }


        public void InsertUnmatchedDocumentType(string document_type)
        {
            using (var conn = new NpgsqlConnection(_biolincc_connString))
            {
                string sql_string = @"INSERT INTO pp.document_types(resource_name) 
                              SELECT '" + document_type + @"'
                              WHERE NOT EXISTS (
                                 SELECT id FROM pp.document_types 
                                 WHERE resource_name = '" + document_type + @"'
                               );";
                conn.Execute(sql_string);
            }
        }
    }
}
