using System;
using System.IO;
using System.Xml.Serialization;

namespace DataDownloader
{
    public class FileWriter
    {
		DataLayer repo;

		public FileWriter(DataLayer _repo)
		{
			repo = _repo;
		}


		public void WriteBioLINCCFile(XmlSerializer writer, BioLINCC_Record st, string full_path)
		{
			if (File.Exists(full_path))
			{
				File.Delete(full_path);
			}
			FileStream file = System.IO.File.Create(full_path);
			writer.Serialize(file, st);
			file.Close();
		}


		public void WriteYodaFile(XmlSerializer writer, Yoda_Record st, string full_path)
		{
			if (File.Exists(full_path))
			{
				File.Delete(full_path);
			}
			FileStream file = System.IO.File.Create(full_path);
			writer.Serialize(file, st);
			file.Close();
		}


		public void UpdateDownloadLog(int seqnum, int source_id, string sd_id, string remote_url,
						 int last_sf_id, DateTime? last_revised_date, string full_path)
		{
			// Get the source data record and modify it
			// or add a new one...
			FileRecord file_record = repo.FetchFileRecord(sd_id, source_id);

			if (file_record == null)
			{
				// this neeeds to have a new record
				// check last revised date....???
				// new record
				file_record = new FileRecord(source_id, sd_id, remote_url, last_sf_id,
												last_revised_date, full_path);
				repo.InsertFileRec(file_record);
			}
			else
			{
				// update record
				file_record.remote_url = remote_url;
				file_record.last_sf_id = last_sf_id;
				file_record.last_revised = last_revised_date;
				file_record.download_status = 2;
				file_record.download_datetime = DateTime.Now;
				file_record.local_path = full_path;

				// Update file record
				repo.StoreFileRec(file_record);
			}

			Console.WriteLine(seqnum.ToString());
		}
	}
}
