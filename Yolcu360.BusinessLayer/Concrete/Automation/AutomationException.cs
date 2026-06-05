using Yolcu360.BusinessLayer.Abstract.Automation;
using Yolcu360.BusinessLayer.Abstract.Browser;
using Yolcu360.Common.Automation;
namespace Yolcu360.BusinessLayer.Concrete.Automation
{
    /// <summary>Otomasyon (arama/kazıma/giriş) sırasında kullanıcıya gösterilebilecek anlamlı hata.</summary>
    public class AutomationException : Exception
    {
        public AutomationException(string message) : base(message) { }
    }
}
