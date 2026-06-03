using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.Common.Logging;
using Yolcu360.DataAccessLayer.Abstract;
using Yolcu360.DtoLayer.ProfileDto;
using Yolcu360.DtoLayer.SearchDto;
using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.BusinessLayer.Concrete
{
    public class SearchProfileManager : ISearchProfileService
    {
        private readonly ISearchProfileRepository _repository;

        public SearchProfileManager(ISearchProfileRepository repository)
        {
            _repository = repository;
        }

        public async Task<int> SaveAsync(string profileName, SearchRequestDto request, CancellationToken ct = default)
        {
            var entity = new SearchProfile
            {
                ProfileName = profileName,
                PickupLocation = request.PickupLocation,
                PickupDateTime = request.PickupDateTime,
                ReturnDateTime = request.ReturnDateTime,
                CreatedAt = DateTime.Now
            };
            var id = await _repository.InsertAsync(entity, ct);
            LogHelper.Info($"Arama profili kaydedildi: '{profileName}' (Id={id}).");
            return id;
        }

        public async Task<int> OverwriteAsync(string profileName, SearchRequestDto request, CancellationToken ct = default)
        {
            var existing = await _repository.GetByNameAsync(profileName, ct);
            if (existing != null)
                await _repository.DeleteAsync(existing.Id, ct);
            return await SaveAsync(profileName, request, ct);
        }

        public async Task<bool> ExistsAsync(string profileName, CancellationToken ct = default)
            => await _repository.GetByNameAsync(profileName, ct) != null;

        public async Task<List<SearchProfileDto>> GetAllAsync(CancellationToken ct = default)
        {
            var profiles = await _repository.GetAllAsync(ct);
            return profiles
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new SearchProfileDto
                {
                    Id = p.Id,
                    ProfileName = p.ProfileName,
                    PickupLocation = p.PickupLocation,
                    PickupDateTime = p.PickupDateTime,
                    ReturnDateTime = p.ReturnDateTime,
                    CreatedAt = p.CreatedAt
                })
                .ToList();
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            await _repository.DeleteAsync(id, ct);
            LogHelper.Info($"Arama profili silindi (Id={id}).");
        }
    }
}
