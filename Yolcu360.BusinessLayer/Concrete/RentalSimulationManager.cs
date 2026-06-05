using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.Common.Logging;
using Yolcu360.DataAccessLayer.Abstract;
using Yolcu360.DtoLayer.SimulationDto;
using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.BusinessLayer.Concrete
{
    public class RentalSimulationManager : IRentalSimulationService
    {
        private readonly ISimulatedRentalRepository _repository;

        public RentalSimulationManager(ISimulatedRentalRepository repository)
        {
            _repository = repository;
        }

        public async Task<RentalSimulationSummaryDto> CreateRentalAsync(CreateSimulatedRentalDto dto, CancellationToken ct = default)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            LogHelper.Info("Kiralama simülasyonu başlatıldı (araç bilgisi alındı).");

            var days = SimulationCalculator.RentalDays(dto.PickupDateTime, dto.ReturnDateTime);
            var extrasTotal = SimulationCalculator.ExtrasTotal(dto.Extras);
            var grandTotal = SimulationCalculator.GrandTotal(dto.BasePrice, extrasTotal);
            LogHelper.Info($"Ek hizmetler hesaplandı ({dto.Extras?.Count ?? 0} adet, toplam {extrasTotal:N2}).");

            var now = DateTime.Now;
            var code = await GenerateRentalCodeAsync(now, ct);

            var entity = new SimulatedRental
            {
                CarResultId = dto.CarResultId,
                CarModel = dto.CarModel,
                RentalCompany = dto.RentalCompany,
                TransmissionType = dto.TransmissionType,
                FuelType = dto.FuelType,
                Segment = dto.Segment,
                PickupLocation = dto.PickupLocation,
                PickupDateTime = dto.PickupDateTime,
                ReturnDateTime = dto.ReturnDateTime,
                RentalDays = days,
                BasePrice = dto.BasePrice,
                ExtraServicesTotal = extrasTotal,
                GrandTotal = grandTotal,
                PaymentMethod = dto.PaymentMethod,
                DriverFirstName = dto.Driver?.FirstName,
                DriverLastName = dto.Driver?.LastName,
                DriverPhone = dto.Driver?.Phone,
                DriverEmail = dto.Driver?.Email,
                DriverIdentityNo = dto.Driver?.IdentityNo,
                DriverBirthDate = dto.Driver?.BirthDate ?? DateTime.MinValue,
                DriverLicenseNo = dto.Driver?.LicenseNo,
                DriverLicenseDate = dto.Driver?.LicenseDate ?? DateTime.MinValue,
                SimulationStatus = "Tamamlandı",
                SimulationCode = code,
                CreatedAt = now
            };

            var extras = (dto.Extras ?? new())
                .Select(e => new SimulatedRentalExtra { ExtraName = e.ExtraName, ExtraPrice = e.ExtraPrice })
                .ToList();

            var id = await _repository.CreateRentalWithExtrasAsync(entity, extras, ct);
            LogHelper.Info($"Simülasyon tamamlandı ve DB'ye kaydedildi (Id={id}, kod={code}, araç='{dto.CarModel}', genel toplam={grandTotal:N2}).");

            return new RentalSimulationSummaryDto
            {
                SimulationCode = code,
                CarModel = dto.CarModel,
                RentalCompany = dto.RentalCompany,
                TransmissionType = dto.TransmissionType,
                FuelType = dto.FuelType,
                Segment = dto.Segment,
                ImageUrl = dto.ImageUrl,
                PickupLocation = dto.PickupLocation,
                PickupDateTime = dto.PickupDateTime,
                ReturnDateTime = dto.ReturnDateTime,
                RentalDays = days,
                Driver = dto.Driver,
                Extras = dto.Extras ?? new(),
                BasePrice = dto.BasePrice,
                ExtraServicesTotal = extrasTotal,
                GrandTotal = grandTotal,
                PaymentMethod = dto.PaymentMethod,
                SimulationDate = now
            };
        }

        public async Task<List<ResultSimulatedRentalDto>> GetAllRentalAsync(CancellationToken ct = default)
        {
            var rows = await _repository.GetAllAsync(ct);
            return rows
                .OrderByDescending(r => r.CreatedAt)
                .Select(MapToDto)
                .ToList();
        }

        public async Task<ResultSimulatedRentalDto> GetByIdRentalAsync(int id, CancellationToken ct = default)
        {
            var entity = await _repository.GetByIdAsync(id, ct);
            return entity == null ? null : MapToDto(entity);
        }

        public async Task<ResultSimulatedRentalDto> GetRentalWithExtrasAsync(int id, CancellationToken ct = default)
        {
            var entity = await _repository.GetRentalWithExtrasAsync(id, ct);
            return entity == null ? null : MapToDto(entity);
        }

        public async Task DeleteRentalAsync(int id, CancellationToken ct = default)
        {
            await _repository.DeleteAsync(id, ct);
            LogHelper.Info($"Simülasyon silindi (Id={id}).");
        }

        private async Task<string> GenerateRentalCodeAsync(DateTime when, CancellationToken ct)
        {
            int seq;
            try { seq = await _repository.RentalCountByDateAsync(when, ct) + 1; }
            catch { seq = 1; }
            var code = $"Y360-SIM-{when:yyyyMMdd}-{seq:D4}";
            LogHelper.Info($"Simülasyon kodu üretildi: {code}.");
            return code;
        }

        private static ResultSimulatedRentalDto MapToDto(SimulatedRental e) => new()
        {
            Id = e.Id,
            CarResultId = e.CarResultId,
            CarModel = e.CarModel,
            RentalCompany = e.RentalCompany,
            TransmissionType = e.TransmissionType,
            FuelType = e.FuelType,
            Segment = e.Segment,
            PickupLocation = e.PickupLocation,
            PickupDateTime = e.PickupDateTime,
            ReturnDateTime = e.ReturnDateTime,
            RentalDays = e.RentalDays,
            BasePrice = e.BasePrice,
            ExtraServicesTotal = e.ExtraServicesTotal,
            GrandTotal = e.GrandTotal,
            PaymentMethod = e.PaymentMethod,
            DriverFirstName = e.DriverFirstName,
            DriverLastName = e.DriverLastName,
            DriverPhone = e.DriverPhone,
            DriverEmail = e.DriverEmail,
            DriverIdentityNo = e.DriverIdentityNo,
            DriverBirthDate = e.DriverBirthDate,
            DriverLicenseNo = e.DriverLicenseNo,
            DriverLicenseDate = e.DriverLicenseDate,
            SimulationStatus = e.SimulationStatus,
            SimulationCode = e.SimulationCode,
            CreatedAt = e.CreatedAt,
            Extras = (e.Extras ?? new())
                .Select(x => new SimulatedRentalExtraDto { ExtraName = x.ExtraName, ExtraPrice = x.ExtraPrice })
                .ToList()
        };
    }
}
