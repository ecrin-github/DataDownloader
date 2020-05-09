using ScrapySharp.Network;
using System;
using System.Text;
using static System.Console;

namespace DataDownloader
{
	class Program
	{
		static ScrapingBrowser browser = new ScrapingBrowser();

		static void Main(string[] args)
		{

			int last_sf_id  = 0;
			int source_id = 0;
			if (args.Length == 2)
			{
                string source = args[0];
				switch (source.ToLower()[0])
				{
					case 'b':
						{
							source_id = 100900; break; // biolincc
						}
					case 'y':
						{
							source_id = 100901; break; // yoda
						}
				}

				if (source_id == 0)
				{
                    WriteLine("sorry - I don't recognise that source argument");
				}
				
				
				string last_sf = args[1];
				if (Int32.TryParse(last_sf, out int sf_id))
				{
					if (sf_id < 100014)
					{
						WriteLine("The second integer needs to be an integer, greater than 100014");
					}
					else
					{
						last_sf_id = sf_id;
					}
				}
				else
				{
					WriteLine("The second integer needs to be an integer, greater than 100015");
				}
			}
			else
			{
				WriteLine("Wrong number of command line arguments - two are required");
				WriteLine("The first a string to indicate the source");
				WriteLine("The second an integer to indicate the last search-fetch id");
			}

            // proceed if both required parameters are valid
			if (last_sf_id > 0 && source_id > 0)
			{

				browser.AllowAutoRedirect = true;
				browser.AllowMetaRedirect = true;
				browser.Encoding = Encoding.UTF8;
				DataLayer repo = new DataLayer();

				switch (source_id)
				{
					case 100900:
						{
							BioLINCC_Controller biolincc_controller = new BioLINCC_Controller(browser, repo, last_sf_id, source_id);
							biolincc_controller.LoopThroughPages();
							break;
						}
					case 100901:
						{
							Yoda_Controller yoda_controller = new Yoda_Controller(browser, repo, last_sf_id, source_id);
							yoda_controller.LoopThroughPages();
							break;
						}
				}
			}
		}
	}

}
