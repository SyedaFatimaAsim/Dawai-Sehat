namespace DawaiSehat.Api.Messaging
{
    namespace DawaiSehat.Api.Messaging
    {
        public class WhatsappOptions
        {
            public bool UseFake { get; set; } = true;
            public string Token { get; set; } = "";
            public string PhoneNumberId { get; set; } = "";
            public string ApiVersion { get; set; } = "v25.0";
            public string VerifyToken { get; set; } = "";
        }
    }
}
