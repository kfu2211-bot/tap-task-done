using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using tts.Data;

var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseMySql("Server=127.0.0.1;Database=unused;User ID=unused",new MySqlServerVersion(new Version(8,0,36))).Options;
using var db = new ApplicationDbContext(options);
var store = new AccountStore(db);
var user = new AppUser {Id=1};
store.SetPassword(user,"Original-test-42!");
var before=AccountStore.SessionFingerprint(user.PasswordHash);
store.SetPassword(user,"Updated-test-64!");
var reloaded = new AppUser {Id=1,PasswordHash=user.PasswordHash!.ToArray()};
if(!store.VerifyPassword(reloaded,"Updated-test-64!") || store.VerifyPassword(reloaded,"Original-test-42!")) throw new Exception("Reset/login password hashing mismatch.");
Console.WriteLine("PASS: replacement hash accepts new password and rejects original password.");
if(AccountStore.SessionFingerprint(reloaded.PasswordHash)==before)throw new Exception("Old session version still valid.");
Console.WriteLine("PASS: replacing password changes the session version.");
if(store.VerifyPassword(new AppUser {PasswordHash=Encoding.UTF8.GetBytes("truncated")},"Updated-test-64!"))throw new Exception("Malformed storage was accepted.");
Console.WriteLine("PASS: malformed/truncated password storage fails closed.");
if(NewPasswordPolicy.IsValid("onlylowercase") || !NewPasswordPolicy.IsValid("Updated-test-64!"))throw new Exception("Password checklist policy mismatch.");
Console.WriteLine("PASS: reset password policy matches the checklist requirements.");
if(!args.Contains("--inspect-account")) return;
var index=Array.IndexOf(args,"--inspect-account");
if(index+1>=args.Length)throw new Exception("Supply the account email to inspect.");
var config=new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json",true);
if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")=="Development")config.AddJsonFile("appsettings.Local.json",true);
var settings=config.AddEnvironmentVariables().Build();
var connection=settings.GetConnectionString("ApplicationDatabase");
if(string.IsNullOrWhiteSpace(connection)){Console.WriteLine("ACCOUNT CHECK unavailable: no database connection is configured in this environment.");return;}
using var live=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseMySql(connection,new MySqlServerVersion(new Version(8,0,36))).Options);
try {
 var email=args[index+1].Trim();
 var row=await live.Users.AsNoTracking().Where(u=>u.Username==email).Select(u=>new {u.Id,u.Username,u.PasswordHash}).SingleOrDefaultAsync();
 if(row is null){Console.WriteLine("ACCOUNT CHECK: supplied email was not found in the configured database.");return;}
 var verified=await live.VerifiedEmails.AnyAsync(v=>v.UserId==row.Id&&v.Email==row.Username);
 bool format=false;
 try {var hash=Convert.FromBase64String(new UTF8Encoding(false,true).GetString(row.PasswordHash??[]));format=hash.Length>=49&&hash[0] is 0 or 1;}catch(FormatException){}catch(DecoderFallbackException){}
 Console.WriteLine($"ACCOUNT CHECK: found=true, emailVerified={verified}, supportedPasswordHashFormat={format}. Password correctness cannot be checked without the password; no data was changed.");
}catch(Exception ex)when(ex is MySqlConnector.MySqlException or InvalidOperationException){Console.WriteLine("ACCOUNT CHECK unavailable: "+ex.GetType().Name);Environment.ExitCode=1;}
