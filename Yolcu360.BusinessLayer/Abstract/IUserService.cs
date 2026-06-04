using Yolcu360.DtoLayer.UserDto;
using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.BusinessLayer.Abstract
{
    /// <summary>
    /// Yerel uygulama kullanıcısı işlemleri. Yolcu360 site girişiyle ilgisi yoktur.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Yeni kullanıcı kaydeder (e-posta + şifre + telefon). Şifre DB'ye HASH'lenerek saklanır.
        /// Boş alan, geçersiz e-posta veya zaten var olan e-posta durumunda kullanıcıya gösterilebilir
        /// mesajla <c>AuthenticationException</c> fırlatır. Oluşan kullanıcının Id'sini döner.
        /// </summary>
        Task<int> RegisterAsync(RegisterUserDto dto, CancellationToken ct = default);
        Task<User> ValidateAsync(LoginUserDto dto, CancellationToken ct = default);
        Task<List<User>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// E-posta/şifre ile uygulama girişi yapar. Doğrulama başarılıysa aktif kullanıcının
        /// kimliği ve Yolcu360 telefon numarası ile <see cref="AuthenticatedUserDto"/> döner.
        /// Başarısızlıkta kullanıcıya gösterilebilecek anlamlı bir mesajla
        /// <c>AuthenticationException</c> fırlatır (boş alan, kullanıcı yok, şifre hatalı,
        /// telefon yok, veritabanı erişilemedi).
        /// </summary>
        Task<AuthenticatedUserDto> LoginAsync(LoginUserDto dto, CancellationToken ct = default);

        /// <summary>E-postaya göre kullanıcı getirir; yoksa null döner.</summary>
        Task<User> GetUserByEmailAsync(string email, CancellationToken ct = default);

        /// <summary>
        /// Girilen şifrenin, veritabanında saklanan HASH ile eşleşip eşleşmediğini döner (PBKDF2,
        /// sabit-zamanlı karşılaştırma). Saklanan değer geçerli bir hash değilse (ör. eski düz metin
        /// kayıt) false döner; bu kullanıcılar yeniden kayıt olmalıdır (hash-only politikası).
        /// </summary>
        bool ValidatePassword(string inputPassword, string storedPasswordOrHash);

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
