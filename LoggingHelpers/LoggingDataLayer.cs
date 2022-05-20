using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PostgreSQLCopyHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DataDownloader
{
    public class LoggingDataLayer
    {
        private string connString;
        private Source source;
        private string sql_file_select_string;
        private string logfile_startofpath;
        private string logfile_path;
        private string pubmed_api_key;
        private StreamWriter sw;

        /// <summary>
        /// Parameterless constructor is used to automatically build
        /// the connection string, using an appsettings.json file that 
        /// has the relevant credentials (but which is not stored in GitHub).
        /// </summary>
        /// 
        public LoggingDataLayer()
        {
            IConfigurationRoot settings = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder();
            builder.Host = settings["host"];
            builder.Username = settings["user"];
            builder.Password = settings["password"];

            builder.Database = "mon";
            connString = builder.ConnectionString;

            logfile_startofpath = settings["logfilepath"];
            pubmed_api_key = settings["pubmed_api_key"];

            sql_file_select_string = "select id, source_id, sd_id, remote_url, last_revised, ";
            sql_file_select_string += " assume_complete, download_status, local_path, last_saf_id, last_downloaded, ";
            sql_file_select_string += " last_harvest_id, last_harvested, last_import_id, last_imported ";
        }

        // Used to check if a log file with a named source has been created
        public string LogFilePath => logfile_path;

        public string PubmedAPIKey => pubmed_api_key;


        public Source FetchSourceParameters(int source_id)
        {
            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                source = Conn.Get<Source>(source_id);
                return source;
            }
        }


        public void OpenLogFile(string source_file_name)
        {
            string dt_string = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture)
                              .Replace("-", "").Replace(":", "").Replace("T", " ");
            if (source_file_name != null)
            {
                string file_name = source_file_name.Substring(source_file_name.LastIndexOf("\\") + 1);
                logfile_path += logfile_startofpath + "DL " + source.database_name + " " + dt_string 
                                           + " USING " + file_name + ".log";
            }
            else
            {
                logfile_path += logfile_startofpath + "DL " + source.database_name + " " + dt_string + ".log";
                
            }
            sw = new StreamWriter(logfile_path, true, System.Text.Encoding.UTF8);
        }

        public void OpenNoSourceLogFile()
        {
            string dt_string = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture)
                              .Replace("-", "").Replace(":", "").Replace("T", " ");
            logfile_path += logfile_startofpath + "DL No Source " + dt_string + ".log";
            sw = new StreamWriter(logfile_path, true, System.Text.Encoding.UTF8);
        }

        public void LogLine(string message, string identifier = "")
        {
            string dt_string = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
            string feedback = dt_string + message + identifier;
            Transmit(feedback);
         }

        public void LogHeader(string message)
        {
            string dt_string = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
            string header = dt_string + "**** " + message + " ****";
           Transmit("");
            Transmit(header);
        }

        public void LogError(string message)
        {
            string dt_string = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
            string error_message = dt_string + "***ERROR*** " + message;
            Transmit("");
            Transmit("+++++++++++++++++++++++++++++++++++++++");
            Transmit(error_message);
            Transmit("+++++++++++++++++++++++++++++++++++++++");
            Transmit("");
        }

        public void LogRes(DownloadResult res)
        {
            string dt_string = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
            Transmit("");
            Transmit(dt_string + "**** " + "Download Result" + " ****");
            Transmit(dt_string + "**** " + "Records checked: " + res.num_checked.ToString() + " ****");
            Transmit(dt_string + "**** " + "Records downloaded: " + res.num_downloaded.ToString() + " ****");
            Transmit(dt_string + "**** " + "Records added: " + res.num_added.ToString() + " ****");
        }

        public void CloseLog()
        {
            LogHeader("Closing Log");
            sw.Flush();
            sw.Close();
        }

        private void Transmit(string message)
        {
            sw.WriteLine(message);
            Console.WriteLine(message);
        }

        public string LogArgsParameters(Args args)
        {

            LogLine("****** DOWNLOAD ******");
            LogHeader("Set up");
            LogLine("source_id is " + args.source_id.ToString());
            LogLine("type_id is " + args.type_id.ToString());
            string file_name = (args.file_name == null) ? " was not provided" : " is " + args.file_name;
            LogLine("file_name" + file_name);
            string cutoff_date = (args.cutoff_date == null) ? " was not provided" : " is " + ((DateTime)args.cutoff_date).ToShortDateString();
            LogLine("cutoff_date" + cutoff_date);
            string filter_id = (args.filter_id == null) ? " was not provided" : " is " + args.filter_id.ToString();
            LogLine("filter" + filter_id);
            string ignore_recent_days = (args.skip_recent_days == null) ? " was not provided" : " is " + args.skip_recent_days.ToString();
            LogLine("ignore recent downloads parameter" + ignore_recent_days);

            string previous_saf_ids = "";
            if (args.previous_searches.Count() > 0)
            {
                foreach (int i in args.previous_searches)
                {
                    LogLine("previous_search is " + i.ToString());
                    previous_saf_ids += ", " + i.ToString();
                }
                previous_saf_ids = previous_saf_ids.Substring(2);
            }
            string no_logging = (args.filter_id == null) ? " was not provided" : " is " + args.no_logging;
            LogLine("no_Logging" + no_logging);
            return previous_saf_ids;
        }


        public DateTime? ObtainLastDownloadDate(int source_id)
        {
            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                string sql_string = "select max(time_started) from sf.saf_events ";
                sql_string += " where source_id = " + source_id.ToString(); 
                DateTime last_download_dt = Conn.ExecuteScalar<DateTime>(sql_string);
                if (last_download_dt != null)
                {
                    return last_download_dt.Date;
                }
                else
                {
                    return null;
                }
            }
        }


        public DateTime? ObtainLastDownloadDateWithFilter(int source_id, int filter_id)
        {
            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                string sql_string = "select max(time_started) from sf.saf_events ";
                sql_string += " where source_id = " + source_id.ToString();
                sql_string += " and filter_id = " + filter_id.ToString();
                DateTime last_download_dt = Conn.ExecuteScalar<DateTime>(sql_string);
                if (last_download_dt != null)
                {
                    return last_download_dt.Date;
                }
                else
                {
                    return null;
                }
            }
        }


        public SFType FetchTypeParameters(int sftype_id)
        {
            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                return Conn.Get<SFType>(sftype_id);
            }
        }


        public int GetNextSearchFetchId()
        {
            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                string sql_string = "select max(id) from sf.saf_events ";
                int last_id = Conn.ExecuteScalar<int>(sql_string);
                return last_id + 1;
            }

        }


        public StudyFileRecord FetchStudyFileRecord(string sd_id, int source_id)
        {
            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                string sql_string = sql_file_select_string;
                sql_string += " from sf.source_data_studies ";
                sql_string += " where sd_id = '" + sd_id + "' and source_id = " + source_id.ToString();
                return Conn.Query<StudyFileRecord>(sql_string).FirstOrDefault();
            }
        }

        
        public ObjectFileRecord FetchObjectFileRecord(string sd_id, int source_id)
        {
            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                string sql_string = sql_file_select_string;
                sql_string += " from sf.source_data_objects ";
                sql_string += " where sd_id = '" + sd_id + "' and source_id = " + source_id.ToString();
                return Conn.Query<ObjectFileRecord>(sql_string).FirstOrDefault();
            }
        }
        

        // used for biolincc only
        public IEnumerable<StudyFileRecord> FetchStudyFileRecords(int source_id)
        {
            string sql_string = "select id, sd_id, local_path ";
            sql_string += " from sf.source_data_studies ";
            sql_string += " where source_id = " + source_id.ToString();
            sql_string += " order by local_path";

            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                return Conn.Query<StudyFileRecord>(sql_string);
            }
        }


        public int InsertSAFEventRecord(SAFEvent saf)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                return (int)conn.Insert<SAFEvent>(saf);
            }
        }


        public bool StoreStudyFileRec(StudyFileRecord file_record)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                return conn.Update<StudyFileRecord>(file_record);
            }
        }


        public bool StoreObjectFileRec(ObjectFileRecord file_record)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                return conn.Update<ObjectFileRecord>(file_record);
            }
        }


        public int InsertStudyFileRec(StudyFileRecord file_record)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                return (int)conn.Insert<StudyFileRecord>(file_record);
            }
        }

        public int InsertObjectFileRec(ObjectFileRecord file_record)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                return (int)conn.Insert<ObjectFileRecord>(file_record);
            }
        }
        
        /*
        public bool UpdateStudyDownloadLogWithCompStatus(int source_id, string sd_id, string remote_url,
                         int saf_id, bool? assume_complete, string full_path)
        {
            bool added = false; // indicates if a new record or update of an existing one

            // Get the source data record and modify it
            // or add a new one...
            StudyFileRecord file_record = FetchStudyFileRecord(sd_id, source_id);
            try
            {
                if (file_record == null)
                {
                    // this neeeds to have a new record
                    // check last revised date....???
                    // new record
                    file_record = new StudyFileRecord(source_id, sd_id, remote_url, saf_id,
                                                    assume_complete, full_path);
                    InsertStudyFileRec(file_record);
                    added = true;
                }
                else
                {
                    // update record
                    file_record.remote_url = remote_url;
                    file_record.last_saf_id = saf_id;
                    file_record.assume_complete = assume_complete;
                    file_record.download_status = 2;
                    file_record.last_downloaded = DateTime.Now;
                    file_record.local_path = full_path;

                    // Update file record
                    StoreStudyFileRec(file_record);
                }

                return added;
            }
            catch (Exception e)
            {
                LogError("In UpdateStudyDownloadLogWithCompStatus: " + e.Message);
                return false;
            }
        }
        */

        public bool UpdateStudyDownloadLog(int source_id, string sd_id, string remote_url,
                         int saf_id, DateTime? last_revised_date, string full_path)
        {
            bool added = false; // indicates if a new record or update of an existing one

            // Get the source data record and modify it
            // or add a new one...
            StudyFileRecord file_record = FetchStudyFileRecord(sd_id, source_id);
            try
            {
                if (file_record == null)
                {
                    // this neeeds to have a new record
                    // check last revised date....???
                    // new record
                    file_record = new StudyFileRecord(source_id, sd_id, remote_url, saf_id,
                                                    last_revised_date, full_path);
                    InsertStudyFileRec(file_record);
                    added = true;
                }
                else
                {
                    // update record
                    file_record.remote_url = remote_url;
                    file_record.last_saf_id = saf_id;
                    file_record.last_revised = last_revised_date;
                    file_record.download_status = 2;
                    file_record.last_downloaded = DateTime.Now;
                    file_record.local_path = full_path;

                    // Update file record
                    StoreStudyFileRec(file_record);
                }

                return added;
            }
            catch(Exception e)
            {
                LogError("In UpdateStudyDownloadLog: " + e.Message);
                return false;
            }
        }


        public bool UpdateObjectDownloadLog(int source_id, string sd_id, string remote_url,
                         int saf_id, DateTime? last_revised_date, string full_path)
        {
            bool added = false; // indicates if a new record or update of an existing one

            // Get the source data record and modify it
            // or add a new one...
            ObjectFileRecord file_record = FetchObjectFileRecord(sd_id, source_id);

            if (file_record == null)
            {
                // this neeeds to have a new record
                // check last revised date....???
                // new record
                file_record = new ObjectFileRecord(source_id, sd_id, remote_url, saf_id,
                                                last_revised_date, full_path);
                InsertObjectFileRec(file_record);
                added = true;
            }
            else
            {
                // update record
                file_record.remote_url = remote_url;
                file_record.last_saf_id = saf_id;
                file_record.last_revised = last_revised_date;
                file_record.download_status = 2;
                file_record.last_downloaded = DateTime.Now;
                file_record.local_path = full_path;

                // Update file record
                StoreObjectFileRec(file_record);
            }

            return added;
        }

        public bool Downloaded_recently(int source_id, string sd_sid, int days_ago)
        {
            string sql_string = @"select id from sf.source_data_studies
                    where last_downloaded::date >= now()::date - " + days_ago.ToString() + @"
                    and sd_id = '" + sd_sid + @"'
                    and source_id = " + source_id.ToString();
            using (var conn = new NpgsqlConnection(connString))
            {
                return conn.Query<int>(sql_string).FirstOrDefault() > 0 ? true : false;
            }
        }


        public ulong StoreStudyRecs(PostgreSQLCopyHelper<StudyFileRecord> copyHelper, IEnumerable<StudyFileRecord> entities)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                // Returns count of rows written 
                return copyHelper.SaveAll(conn, entities);
            }
        }


        public ulong StoreObjectRecs(PostgreSQLCopyHelper<ObjectFileRecord> copyHelper, IEnumerable<ObjectFileRecord> entities)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                // Returns count of rows written 
                return copyHelper.SaveAll(conn, entities);
            }
        }


        // Stores an 'extraction note', e.g. an unusual occurence or error found and
        // logged during the extraction, in the associated table.

        public void StoreExtractionNote(ExtractionNote ext_note)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Insert<ExtractionNote>(ext_note);
            }
        }

    }
}

