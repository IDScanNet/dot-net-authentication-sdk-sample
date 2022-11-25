using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IDScanNet.Authentication;
using IDScanNet.Authentication.SDK;
using Newtonsoft.Json;

namespace TestApp
{
    public class Program
    {
        private static AuthenticationService _AuthenticationService;

        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);

            Stopwatch sw = Stopwatch.StartNew();
            //Init AuthenticationService
            Console.WriteLine("Init started");
            try
            {
                await Init();
                sw.Stop();
                Console.WriteLine("Init time " + sw.ElapsedMilliseconds);
                //Authenticate NoCroped document
                await Authenticate("NoCropped", true);
                //Authenticate valid document
                await Authenticate("Valid");
                //Authenticate fake by UV
                await Authenticate("UVFailed");
                //Authenticate fake RawString
                await Authenticate("FakeByRawString");
                //Authenticate face
                await Authenticate("Face");
                //Authenticate DL front
                await Authenticate("DLFront");
                //Authenticate DL back
                await Authenticate("DLBack");
                //Authenticate passport front
                await Authenticate("PassportFront");
                //Authenticate spoofing fake
                await Authenticate("BNWAntiSpoofing");
                //Authenticate RFID
                await Authenticate("Rfid");
                //Authenticate by UV
                await Authenticate("UVFailed",false, AuthenticationTestType.UVMark);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            Console.ReadLine();
        }

        private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            _AuthenticationService.Dispose();
        }


        private static async Task Init()
        {
            Directory.CreateDirectory("Authentication Logs");
            //Simple Authentication settings
            var AuthenticationServiceSettings = new AuthenticationServiceSettings
            {
                //Set logging directory(default is c:\Users\Public\Documents\IDScan.net\Authentication.SDK\Logs\)
                LoggingDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Authentication Logs"),
                HostDataDirectoryPath = @"C:\ProgramData\IDScan.net\IDScanNet.Authentication.SDK.Data",

                //Advanced Authentication settings:
                //Host - pipe name for connection. If it's not stated, then the default one will be used
                //Port - ignore this for now, as it would be required sometime in the future
                //HostDirectoryPath = @"c:\Projects\IDScanNet.Authentication.SDK.Host\IDScanNet.Authentication.SDK.Host\bin\Debug\net5.0-windows7\"
                //HostDataDirectoryPath - folder with data-files; can be substituted with a different value in case it's part of an app and data-files are somewhere close
            };

            _AuthenticationService = new AuthenticationService(AuthenticationServiceSettings);

            //Fired when the document processing stage is changed
            _AuthenticationService.ProcessingStageChanged += (sender, s) =>
            {
                Console.WriteLine(s.Status);
            };
            //Fired when the error has occurred
            _AuthenticationService.ErrorReceived += (sender, s) =>
            {
                Console.WriteLine("Error:" + s.Text);
            };

            //Asynchronously initialize Authentication service
            await _AuthenticationService.InitializeAsync();
        }

        private static async Task Authenticate(String folder, Boolean isCropRequired = false, AuthenticationTestType? testType = null)
        {
            Console.WriteLine("----------------------------------------------------------------------------------------");
            Console.WriteLine($"Authenticate {folder}:");
            Stopwatch sw = Stopwatch.StartNew();

            //Create a new Authentication request
            var request = new AuthenticationRequest();
            request.Id = Guid.NewGuid();
            request.Scan = new ScanResult();
            request.IsCropRequired = isCropRequired;
            request.TestType = testType;
            var documentPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder);

            //Set RawString with a RawDataSource type
            var file = Directory.GetFiles(documentPath, "Pdf417RawData.txt").FirstOrDefault();
            if (file != null)
                request.Scan.RawItems = new Dictionary<RawDataSource, RawData>
            {
                {
                    RawDataSource.PDF417, new RawData
                    {
                        RawString = await File.ReadAllTextAsync(file)
                    }
                }
            };
            //Set Images by ImageType
            request.Scan.ScannedImages = new Dictionary<ImageType, byte[]>();

            file = Directory.GetFiles(documentPath, "Normal.*").FirstOrDefault();
            if (file != null)
                request.Scan.ScannedImages.Add(ImageType.ColorFront, await File.ReadAllBytesAsync(file));

            file = Directory.GetFiles(documentPath, "NormalBack.*").FirstOrDefault();
            if (file != null)
                request.Scan.ScannedImages.Add(ImageType.ColorBack, await File.ReadAllBytesAsync(file));

            file = Directory.GetFiles(documentPath, "UV.*").FirstOrDefault();
            if (file != null)
                 request.Scan.ScannedImages.Add(ImageType.UVFront, await File.ReadAllBytesAsync(file));
            
            file = Directory.GetFiles(documentPath, "UVBack.*").FirstOrDefault();
            if (file != null)
                request.Scan.ScannedImages.Add(ImageType.UVBack, await File.ReadAllBytesAsync(file));

            file = Directory.GetFiles(documentPath, "IR.*").FirstOrDefault();
            if (file != null)
                request.Scan.ScannedImages.Add(ImageType.IRFront, await File.ReadAllBytesAsync(file));

            file = Directory.GetFiles(documentPath, "IRBack.*").FirstOrDefault();
            if (file != null)
                request.Scan.ScannedImages.Add(ImageType.IRBack, await File.ReadAllBytesAsync(file));

            file = Directory.GetFiles(documentPath, "Face.*").FirstOrDefault();
            if (file != null)
                request.Scan.ScannedImages.Add(ImageType.CameraFace, await File.ReadAllBytesAsync(file));

            string rFidPath = Path.Combine(documentPath, "rfid");
            if (Directory.Exists(rFidPath))
            {
                RfidData rfid; 
                string rFidjson = Path.Combine(documentPath, "rfid", "rfid.json");
                if (File.Exists(rFidjson))
                {
                    request.Scan.RawItems = new Dictionary<RawDataSource, RawData>();
                    rfid = JsonConvert.DeserializeObject<RfidData>(await File.ReadAllTextAsync(rFidjson));
                    request.Scan.RawItems.Add(RawDataSource.Rfid, new RawData()
                    {
                        RawString = JsonConvert.SerializeObject(rfid)
                    });
                    string rFidPhoto = Path.Combine(documentPath, "rfid", "Face.jpg");
                    if (File.Exists(rFidPhoto))
                        request.Scan.ScannedImages.Add(ImageType.RfidFace, await File.ReadAllBytesAsync(rFidPhoto));
                }
            }

            //Asynchronously authenticate document
            AuthenticationResponse result = await _AuthenticationService.ProcessAsync(request);

            Console.WriteLine();
          
            if (result.Result != null)
            {
                sw.Stop();
                Console.WriteLine($"Authentication Result for {folder} ElapsedMilliseconds: {sw.ElapsedMilliseconds}:");
                String group = "";
                foreach (var testResult in result.Result.Results.OrderBy(x=>x.TestGroup))
                {
                    if (group != testResult.TestGroup.ToString())
                    {
                        group = testResult.TestGroup.ToString();
                        Console.WriteLine($"{group}:");
                    }
                    Console.WriteLine($"    {testResult.Name} - {testResult.Type} {testResult.TestStatus} {testResult.Confidence}");
                    if (testResult.CrossMatches != null)
                        foreach (var match in testResult.CrossMatches)
                        {
                            Console.WriteLine(
                                $"          {match.FieldName} - {match.Item1.DataSourceString} = {match.Item1.Value};  {match.Item2.DataSourceString} = {match.Item2.Value} Confidence = {match.Confidence}");
                        }
                }
                Console.WriteLine();
                //Final Authentication result
                Console.WriteLine("Authentication status = " + result.Result?.AuthenticationStatus);

                Console.WriteLine();
                Console.WriteLine("Document property value: ");
                Console.WriteLine();
                var document = JsonConvert.SerializeObject(result.Document, Formatting.Indented);
                Console.WriteLine(document);
                Console.WriteLine();
                Console.WriteLine("PlainDocument property value: ");
                Console.WriteLine();
                var plainDocument = JsonConvert.SerializeObject(result.PlainDocument, Formatting.Indented);
                Console.WriteLine(plainDocument);

                foreach (var image in result.ProcessedInfo.ProcessedImages)
                {
                    File.WriteAllBytes($"{folder}\\Processed_{image.Key}.jpg", image.Value);
                }
            }
           
            Console.WriteLine();
        }
       

    }
}
