using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.Common.Helpers;
using Yolcu360.Common.Logging;
using Yolcu360.DataAccessLayer.Abstract;
using Yolcu360.DtoLayer.UserDto;
using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.BusinessLayer.Concrete
{
    /// <summary>
    /// Yerel uygulama kullanıcısı işlemleri. NOT: Bu okul projesinde şifreler basitlik
    /// için düz metin saklanır; gerçek bir üründe mutlaka hash'lenmelidir.
    /// </summary>
    public class UserManager : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserManager(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<int> RegisterAsync(RegisterUserDto dto, CancellationToken ct = default)
        {
            var email = (dto?.Email ?? string.Empty).Trim();
            var password = dto?.Password ?? string.Empty;
            var phone = (dto?.PhoneNumber ?? string.Empty).Trim();

            // Doğrulamalar (kullanıcıya gösterilebilir mesajlar).
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                throw new AuthenticationException("E-posta veya şifre boş olamaz.");
            if (!email.Contains('@') || email.Length < 5)
                throw new AuthenticationException("Geçerli bir e-posta adresi girin.");
            if (password.Length < 4)
                throw new AuthenticationException("Şifre en az 4 karakter olmalı.");
            if (string.IsNullOrWhiteSpace(phone))
                throw new AuthenticationException("Telefon numarası boş olamaz.");

            User existing;
            try
            {
                existing = await _userRepository.GetByEmailAsync(email, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogHelper.Error("Kayıt sırasında veritabanına erişilemedi.", ex);
                throw new AuthenticationException("Veritabanı bağlantısı kurulamadı.");
            }

            if (existing != null)
                throw new AuthenticationException("Bu e-posta ile zaten bir kullanıcı var.");

            var user = new User
            {
                Email = email,
                Password = PasswordHasher.Hash(password), // şifre HASH'lenerek saklanır (düz metin değil)
                PhoneNumber = phone,
                CreatedAt = DateTime.Now
            };
            var id = await _userRepository.InsertAsync(user, ct);
            LogHelper.Info($"Yeni kullanıcı kaydedildi: {email} (Id={id}, Tel={PhoneMaskHelper.Mask(phone)}).");
            return id;
        }

        public async Task<User> ValidateAsync(LoginUserDto dto, CancellationToken ct = default)
        {
            var user = await _userRepository.GetByEmailAsync(dto.Email, ct);
            if (user != null && ValidatePassword(dto.Password, user.Password))
                return user;
            return null;
        }

        public Task<User> GetUserByEmailAsync(string email, CancellationToken ct = default)
            => _userRepository.GetByEmailAsync((email ?? string.Empty).Trim(), ct);

        // Hash-only politikası: şifreler PBKDF2 ile hash'lenerek saklanır. Saklanan değer geçerli bir
        // hash değilse (ör. eski düz metin kayıt) doğrulama başarısız olur ve kullanıcı yeniden kayıt
        // olmalıdır. Karşılaştırma sabit-zamanlıdır (bkz. PasswordHasher).
        public bool ValidatePassword(string inputPassword, string storedPasswordOrHash)
            => PasswordHasher.Verify(inputPassword, storedPasswordOrHash);

        /// <summary>
        /// E-posta/şifre ile uygulama girişi. Adımlar: boş alan kontrolü → DB'den kullanıcı →
        /// şifre kontrolü → telefon numarası kontrolü → AuthenticatedUserDto. Telefon numarası
        /// loglanması gerekirse MASKELİ loglanır; şifre/OTP asla loglanmaz.
        /// </summary>
        public async Task<AuthenticatedUserDto> LoginAsync(LoginUserDto dto, CancellationToken ct = default)
        {
            var email = (dto?.Email ?? string.Empty).Trim();
            var password = dto?.Password ?? string.Empty;

            // 1) Boş alan kontrolü.
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                throw new AuthenticationException("E-posta veya şifre boş olamaz.");

            // 2) Kullanıcıyı DB'den bul. Veritabanına erişilemezse anlamlı mesaj ver.
            User user;
            try
            {
                user = await _userRepository.GetByEmailAsync(email, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogHelper.Error("Giriş sırasında veritabanına erişilemedi.", ex);
                throw new AuthenticationException("Veritabanı bağlantısı kurulamadı.");
            }

            // 3) Kullanıcı var mı?
            if (user == null)
                throw new AuthenticationException("Kullanıcı bulunamadı.");

            // 4) Şifre doğru mu?
            if (!ValidatePassword(password, user.Password))
                throw new AuthenticationException("Şifre hatalı.");

            // 5) Bu hesaba bağlı Yolcu360 telefon numarası var mı?
            var phone = (user.PhoneNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(phone))
                throw new AuthenticationException("Bu kullanıcıya ait telefon numarası bulunamadı.");

            // 6) Başarılı. Telefon numarası yalnızca MASKELİ loglanır.
            LogHelper.Info($"Uygulama girişi başarılı: {email} (Id={user.Id}, Tel={PhoneMaskHelper.Mask(phone)}).");
            return new AuthenticatedUserDto
            {
                Id = user.Id,
                Email = user.Email,
                PhoneNumber = phone
            };
        }

        public Task<List<User>> GetAllAsync(CancellationToken ct = default)
            => _userRepository.GetAllAsync(ct);

        public Task<User> GetActiveUserAsync(CancellationToken ct = default)
            => _userRepository.GetFirstAsync(ct);

        public async Task<string> GetSavedPhoneNumberAsync(CancellationToken ct = default)
        {
            var user = await _userRepository.GetFirstAsync(ct);
            return user?.PhoneNumber ?? string.Empty;
        }

        public async Task SavePhoneNumberAsync(string phoneNumber, CancellationToken ct = default)
        {
            phoneNumber = (phoneNumber ?? string.Empty).Trim();

            var user = await _userRepository.GetFirstAsync(ct);
            if (user != null)
            {
                user.PhoneNumber = phoneNumber;
                await _userRepository.UpdateAsync(user, ct);
                LogHelper.Info($"Aktif kullanıcının telefon numarası güncellendi (Id={user.Id}).");
                return;
            }

            // Hiç kullanıcı yoksa, telefon numarasını saklamak için minimal yerel kullanıcı.
            var created = new User
            {
                Email = "local-user@yolcu360.app",
                Password = string.Empty,
                PhoneNumber = phoneNumber,
                CreatedAt = DateTime.Now
            };
            var id = await _userRepository.InsertAsync(created, ct);
            LogHelper.Info($"Telefon numarası için yerel kullanıcı oluşturuldu (Id={id}).");
        }

        public async Task<List<string>> GetAllPhoneNumbersAsync(CancellationToken ct = default)
        {
            var users = await _userRepository.GetAllAsync(ct);
            return users
                .Select(u => (u.PhoneNumber ?? string.Empty).Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
        }

        public async Task AddPhoneNumberAsync(string phoneNumber, CancellationToken ct = default)
        {
            phoneNumber = (phoneNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return;

            var users = await _userRepository.GetAllAsync(ct);
            if (users.Any(u => string.Equals((u.PhoneNumber ?? string.Empty).Trim(), phoneNumber, StringComparison.Ordinal)))
                return; // zaten kayıtlı

            // Telefonu boş olan bir kullanıcı varsa onu kullan; yoksa numara için minimal kullanıcı ekle.
            var emptyUser = users.FirstOrDefault(u => string.IsNullOrWhiteSpace(u.PhoneNumber));
            if (emptyUser != null)
            {
                emptyUser.PhoneNumber = phoneNumber;
                await _userRepository.UpdateAsync(emptyUser, ct);
            }
            else
            {
                var created = new User
                {
                    Email = $"phone-{DateTime.Now:yyyyMMddHHmmssfff}@yolcu360.app",
                    Password = string.Empty,
                    PhoneNumber = phoneNumber,
                    CreatedAt = DateTime.Now
                };
                await _userRepository.InsertAsync(created, ct);
            }
            LogHelper.Info("Yeni giriş numarası kaydedildi.");
        }
    }
}
