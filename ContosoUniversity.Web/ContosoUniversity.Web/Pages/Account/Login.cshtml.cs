using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ContosoUniversity.Data.Models.Account;
using MimeKit;
using ContosoUniversity.Data.Repository;
using ContosoUniversity.Web.Utilities;
using EmailService;
using System.Drawing;

namespace ContosoUniversity.Web.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IAccountRepository _accountRepo;
        private readonly EmailConfiguration _emailConfig;
        private readonly EmailSender _emailSender;

        [BindProperty]
        public User Credentials { get; set; }

        public LoginModel(IAccountRepository accountRepo,EmailConfiguration emailConfig,EmailSender emailSender)
        {
            _accountRepo = accountRepo;
            _emailConfig = emailConfig;
            _emailSender = emailSender;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            //validation logic goes here

            if (Credentials != null)
            {
                //verify credentials
                string MD5Password = Utility.GenerateMD5(Credentials.Password);
                User loggedinUser = _accountRepo.Login(Credentials.UserName, MD5Password);

                if (loggedinUser != null) //user is verified
                {
                    if (_accountRepo.isEmailVerified(loggedinUser))
                    {

                        var claims = new List<Claim>{
                                        new Claim(ClaimTypes.Name, Credentials.UserName),
                                        new Claim(ClaimTypes.Email,loggedinUser.Email)
                                    };

                        //Add all roles
                        foreach (var role in loggedinUser.UserRoles)
                        { 
                            claims.Add(new Claim(ClaimTypes.Role, role.Roles.Name));
                        }

                        ClaimsIdentity identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                        ClaimsPrincipal principal = new ClaimsPrincipal(identity);

                        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                        return Redirect("/Index");
                    }
                    else //user's email is not verified
                    {
                        var uri = $"{Request.Scheme}://{Request.Host}/Account/Verify";

                        string message = $@"<h2 style='color:red;'>Verify your Email address</h2>
                                                   <form method='post' action={uri}
                                                    <div>
                                                        <input type='hidden' value={loggedinUser.ID} name='PostBackHidden' />
                                                        Welcome! {loggedinUser.UserName} , Click on the below link to verify your Account<br/>
                                                        <button type='submit'>Verify your email</button>
                                                    </div></form> ";


                        var emailMessage = new MimeMessage();
                        emailMessage.From.Add(new MailboxAddress("email", _emailConfig.From));
                        emailMessage.To.Add(new MailboxAddress("email",loggedinUser.Email));
                        emailMessage.Subject = "Email account verification";
                        emailMessage.Body = new TextPart(MimeKit.Text.TextFormat.Html)
                        {

                            Text = message
                        };

                        //send email
                        _emailSender.SendEmail(emailMessage);
                    }
                }
            }

            return Page();
        }
        
    }
}
