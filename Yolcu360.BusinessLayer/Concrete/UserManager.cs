using Yolcu360.BusinessLayer.Abstract;
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

        public async Task<int> RegisterAsync(LoginUserDto dto, CancellationToken ct = default)
        {
            var existing = await _userRepository.GetByEmailAsync(dto.Email, ct);
            if (existing != null)
                throw new InvalidOperationException("Bu e-posta ile zaten bir kullanıcı var.");

            var user = new User
            {
                Email = dto.Email,
                Password = dto.Password,
                PhoneNumber = string.Empty,
                CreatedAt = DateTime.Now
            };
            var id = await _userRepository.InsertAsync(user, ct);
            LogHelper.Info($"Yeni yerel kullanıcı kaydedildi: {dto.Email} (Id={id}).");
            return id;
        }

        public async Task<User> ValidateAsync(LoginUserDto dto, CancellationToken ct = default)
        {
            var user = await _userRepository.GetByEmailAsync(dto.Email, ct);
            if (user != null && user.Password == dto.Password)
                return user;
            return null;
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
