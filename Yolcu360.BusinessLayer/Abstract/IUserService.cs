using Yolcu360.DtoLayer.UserDto;
using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.BusinessLayer.Abstract
{
    /// <summary>
    /// Yerel uygulama kullanıcısı işlemleri. Yolcu360 site girişiyle ilgisi yoktur.
    /// </summary>
    public interface IUserService
    {
        Task<int> RegisterAsync(LoginUserDto dto, CancellationToken ct = default);
        Task<User> ValidateAsync(LoginUserDto dto, CancellationToken ct = default);
        Task<List<User>> GetAllAsync(CancellationToken ct = default);

        /// <summary>Varsayılan/aktif yerel kullanıcıyı getirir; yoksa null döner.</summary>
        Task<User> GetActiveUserAsync(CancellationToken ct = default);

        /// <summary>
        /// Aktif kullanıcının Yolcu360 telefon numarasını döner (yoksa boş string).
        /// </summary>
        Task<string> GetSavedPhoneNumberAsync(CancellationToken ct = default);

        /// <summary>
        /// Telefon numarasını yerel kullanıcıya kaydeder. Kullanıcı yoksa telefon için
        /// minimal bir yerel kullanıcı oluşturur. Mevcut alanları bozmaz.
        /// </summary>
        Task SavePhoneNumberAsync(string phoneNumber, CancellationToken ct = default);

        /// <summary>DB'de kayıtlı tüm (boş olmayan, tekilleştirilmiş) telefon numaralarını döner.</summary>
        Task<List<string>> GetAllPhoneNumbersAsync(CancellationToken ct = default);

        /// <summary>
        /// Yeni bir giriş numarasını kaydeder (zaten varsa dokunmaz). Böylece dropdown'da
        /// birden fazla numara seçilebilir hale gelir.
        /// </summary>
        Task AddPhoneNumberAsync(string phoneNumber, CancellationToken ct = default);
    }
}
