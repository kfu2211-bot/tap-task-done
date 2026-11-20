using tts.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;
using System.Text.Json;
void Check(bool pass, string label) { if (!pass) throw new Exception(label); Console.WriteLine("PASS: " + label); }
string[] driver = ["LAND TRANSPORTATION OFFICE", "DRIVER'S LICENSE", "License No. A01-23-456789"];
Check(IdentityIdOcr.Matches("Driver's License", "A0123456789", driver), "full printed number matches ignoring separators");
Check(!IdentityIdOcr.Matches("Driver's License", "99999999999", driver), "random number blocked");
Check(!IdentityIdOcr.Matches("Driver's License", "456789", driver), "partial number blocked");
Check(!IdentityIdOcr.Matches("UMID", "A0123456789", driver), "wrong ID type blocked");
Check(!IdentityIdOcr.Matches("Driver's License", "19990101", [.. driver, "DATE OF BIRTH 19990101"]), "matching digits in birth date do not count as an ID number");
Check(IdentityDocumentTypes.NumberFor("Driver's License").Label == "License No.", "driver's license uses its License No. field");
Check(IdentityIdOcr.Matches("Driver's License", "A0123456789", ["DRIVER'S LICENSE", "License No. Expiration Date Agency Code", "A01-23-456789 2030/06/01 N01"]), "LTO expiry and agency columns are not merged into the license number");
Check(!IdentityIdOcr.Matches("Driver's License", "20300601", ["DRIVER'S LICENSE", "License No. Expiration Date Agency Code", "A01-23-456789 2030/06/01 N01"]), "adjacent expiry cannot be submitted as License No.");
Check(!IdentityIdOcr.Matches("Driver's License", "12345678901", ["DRIVER'S LICENSE", "LTO CLIENT ID 12345678901", "CARD SERIAL NO. 12345678901"]), "LTO client ID and card serial are not license numbers");
Check(IdentityIdOcr.Matches("Philippine National ID", "1234567890123456", ["PAMBANSANG PAGKAKAKILANLAN", "1234-5678-9012-3456"]), "unlabelled standalone PCN on National ID supported");
Check(!IdentityIdOcr.Matches("Philippine National ID", "123456789012", ["PHILSYS", "PSN 1234-5678-9012"]), "private PSN is not accepted as PCN");
Check(IdentityIdOcr.Matches("UMID", "123456789012", ["UNIFIED MULTI-PURPOSE ID", "CRN 1234-5678901-2"]), "UMID CRN label supported");
Check(!IdentityIdOcr.Matches("UMID", "1234567890", ["UMID", "SSS NUMBER 1234567890"]), "SSS membership number is not UMID CRN");
Check(IdentityIdOcr.Matches("PRC ID", "1234567", ["PROFESSIONAL REGULATION COMMISSION", "REGISTRATION NO. 1234567"]), "PRC registration number supported");
Check(IdentityIdOcr.Matches("Postal ID", "123456789012", ["PHILIPPINE POSTAL", "POSTAL REFERENCE NO. 123456789012"]), "Postal Reference No. label supported");
Check(!IdentityIdOcr.Matches("Driver's License", "A0123456789", ["RANDOM PICTURE A01-23-456789"]), "unrelated picture with digits blocked");
Check(IdentityIdOcr.Matches("Philippine Passport", "P1234567A", ["PASSPORT", "Passport No.", "P1234567A"]), "number below its label supported");
Check(!IdentityIdOcr.Matches("Philippine Passport", "P1234567A", ["PASSPORT", "Passport No. P1234567A/1234"]), "number prefix before slash blocked");
Check(!IdentityIdOcr.Matches("Philippine Passport", "P1234567A", ["PASSPORT", "Passport No. P1234567A 1234"]), "number prefix before spaced digits blocked");
Check(IdentityIdOcr.Matches("Philippine Passport", "P1234567A", ["PASSPORT", "Passport No. P 1234567 A"]), "OCR-separated passport prefix and suffix are preserved");
Check(!IdentityIdOcr.Matches("Philippine Passport", "P1234567", ["PASSPORT", "Passport No. P1234567 A"]), "standalone suffix cannot be dropped to match a partial passport number");
var tsv="level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n"+
"5\t1\t1\t1\t1\t1\t0\t0\t0\t0\t99\t1234\n5\t1\t1\t1\t1\t2\t0\t0\t0\t0\t20\t5678\n";
Check(IdentityIdOcr.ConfidentLines(tsv).Length == 0, "low confidence digit discards entire OCR line");
var faintLabel = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n"+
"5\t1\t1\t1\t1\t1\t0\t0\t20\t20\t40\tLicense\n5\t1\t1\t1\t1\t2\t0\t0\t20\t20\t99\tNo.\n5\t1\t1\t1\t1\t3\t0\t0\t20\t20\t99\tA01-23-456789\n";
Check(IdentityIdOcr.Matches("Driver's License","A0123456789", ["DRIVER'S LICENSE",..IdentityIdOcr.ConfidentLines(faintLabel)]),"faint alphabetic label no longer discards a clear complete number");
var protection = new EphemeralDataProtectionProvider();
var ocr = new IdentityIdOcr(protection, NullLogger<IdentityIdOcr>.Instance);
var bytes = new byte[]{1,2,3};
string Token(DateTime expiry) => protection.CreateProtector("tts.identity.ocr.v2").Protect(JsonSerializer.Serialize(new {UserId=42,Type="Driver's License",Number="A0123456789",Hash=Convert.ToHexString(SHA256.HashData(bytes)),Expires=expiry}));
var token=Token(DateTime.UtcNow.AddMinutes(1));
Check(ocr.ValidReceipt(token,42,"Driver's License","A01-23-456789",bytes), "signed receipt accepts unchanged upload");
Check(!ocr.ValidReceipt(token,42,"Driver's License","A0123456789",[1,2,4]), "swapping uploaded ID invalidates receipt");
Check(!ocr.ValidReceipt(token,43,"Driver's License","A0123456789",bytes), "receipt cannot be reused by another account");
Check(!ocr.ValidReceipt(token,42,"UMID","A0123456789",bytes), "changing ID type invalidates receipt");
Check(!ocr.ValidReceipt(token,42,"Driver's License","99999999999",bytes), "changing number invalidates receipt");
Check(!ocr.ValidReceipt(Token(DateTime.UtcNow.AddSeconds(-1)),42,"Driver's License","A0123456789",bytes), "expired receipt rejected");
Check(!ocr.ValidReceipt("invalid",42,"Driver's License","A0123456789",bytes), "unsigned receipt rejected");
var invalid = await ocr.CheckAsync(42,"Driver's License","A0123456789",bytes,"image/png",default);
Check(!invalid.Success && invalid.Token is null, "missing engine or invalid image blocks rather than issuing an unchecked receipt");
Check(IdentityIdOcr.Assess("Driver's License","99999999999",driver)=="mismatch", "readable different license is a hard mismatch");
Check(IdentityIdOcr.Assess("Driver's License","A0123456789",[])=="unreadable", "no confident text is unreadable rather than a claimed mismatch");
string OcrTsv(params string[] lines) {
 var output="level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n";
 for(var i=0;i<lines.Length;i++) { var words=lines[i].Split(' ');for(var j=0;j<words.Length;j++)output+=$"5\t1\t1\t1\t{i+1}\t{j+1}\t0\t0\t20\t20\t99\t{words[j]}\n"; }
 return output;
}
var commands=new List<(string Tool,string[] Args)>();
var simulated=new IdentityIdOcr(protection,NullLogger<IdentityIdOcr>.Instance,(tool,arguments,ct)=>{
 commands.Add((tool,arguments));return Task.FromResult(tool=="tesseract"?OcrTsv(driver):"");
});
var matched=await simulated.CheckAsync(42,"Driver's License","A0123456789",bytes,"image/jpeg",default);
Check(matched.Success && !matched.CanManualReview && ocr.ValidReceipt(matched.Token,42,"Driver's License","A0123456789",bytes), "successful pipeline issues only a matched receipt");
Check(commands[0].Args.Contains("1400x1400>") && commands[0].Args.Contains("-auto-orient") && commands[0].Args.Contains("-limit") && commands[1].Tool=="tesseract", "bounded preprocessing runs before text reading");
var mismatch=await simulated.CheckAsync(42,"Driver's License","99999999999",bytes,"image/jpeg",default);
Check(!mismatch.Success && !mismatch.CanManualReview && mismatch.Token is null,"explicit mismatch never offers fallback or a receipt");
var unreadable=new IdentityIdOcr(protection,NullLogger<IdentityIdOcr>.Instance,(_,_,_)=>Task.FromResult(""));
var manual=await unreadable.CheckAsync(42,"Driver's License","A0123456789",bytes,"image/jpeg",default);
Check(!manual.Success && !manual.CanManualReview && manual.Token is null, "unreadable or random image cannot obtain a manual-review receipt");
var legacy = protection.CreateProtector("tts.identity.ocr.v1").Protect(JsonSerializer.Serialize(new { UserId=42,Type="Driver's License",Number="A0123456789",Hash=Convert.ToHexString(SHA256.HashData(bytes)),Expires=DateTime.UtcNow.AddMinutes(1),Mode="manual" }));
Check(ocr.ReceiptMode(legacy,42,"Driver's License","A0123456789",bytes) is null,"previous manual-review bypass receipt is invalidated");
var forgedMode = protection.CreateProtector("tts.identity.ocr.v2").Protect(JsonSerializer.Serialize(new { UserId=42,Type="Driver's License",Number="A0123456789",Hash=Convert.ToHexString(SHA256.HashData(bytes)),Expires=DateTime.UtcNow.AddMinutes(1),Mode="manual" }));
Check(ocr.ReceiptMode(forgedMode,42,"Driver's License","A0123456789",bytes) is null,"manual mode cannot authorize final submission");
var timeout=new IdentityIdOcr(protection,NullLogger<IdentityIdOcr>.Instance,(_,_,_)=>throw new OperationCanceledException());
var timed=await timeout.CheckAsync(42,"Driver's License","A0123456789",bytes,"image/jpeg",default);
Check(!timed.CanManualReview && !timed.Success && timed.Token is null,"OCR timeout cannot authorize an unchecked submission");
var sparsePasses=0;
var retry=new IdentityIdOcr(protection,NullLogger<IdentityIdOcr>.Instance,(tool,arguments,ct)=>{
 if(tool!="tesseract")return Task.FromResult("");
 sparsePasses++;return Task.FromResult(arguments.Contains("6")?OcrTsv(driver):"");
});
var retried=await retry.CheckAsync(42,"Driver's License","A0123456789",bytes,"image/jpeg",default);
Check(retried.Success && sparsePasses==2,"unreadable sparse result retries block-layout OCR once");
var enhancedPasses = 0;
var enhanced = new IdentityIdOcr(protection, NullLogger<IdentityIdOcr>.Instance, (tool, arguments, ct) => {
 if(tool != "tesseract") return Task.FromResult("");
 enhancedPasses++;
 return Task.FromResult(arguments.Contains("3") ? OcrTsv(driver) : "");
});
var enhancedResult = await enhanced.CheckAsync(42,"Driver's License","A0123456789",bytes,"image/jpeg",default);
Check(enhancedResult.Success && enhancedPasses == 3,"contrast-enhanced OCR can recover a complete number without manual fallback");
