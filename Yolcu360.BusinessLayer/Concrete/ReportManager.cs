using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.Common.Logging;
using Yolcu360.DataAccessLayer.Abstract;
using Yolcu360.DtoLayer.CarResultDto;
using Yolcu360.DtoLayer.ReportDto;
using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.BusinessLayer.Concrete
{
    public class ReportManager : IReportService
    {
        private readonly IReportRepository _reportRepository;
        private readonly ICarResultRepository _carResultRepository;
        private readonly ICarResultService _carResultService;

        public ReportManager(
            IReportRepository reportRepository,
            ICarResultRepository carResultRepository,
            ICarResultService carResultService)
        {
            _reportRepository = reportRepository;
            _carResultRepository = carResultRepository;
            _carResultService = carResultService;
        }

        public async Task<int> SaveReportAsync(CreateReportDto dto, CancellationToken ct = default)
        {
            var report = new Report
            {
                ReportName = dto.ReportName,
                PickupLocation = dto.PickupLocation,
                PickupDateTime = dto.PickupDateTime,
                ReturnDateTime = dto.ReturnDateTime,
                CreatedAt = DateTime.Now
            };

            var cars = _carResultService.ToEntities(dto.Cars);
            var id = await _reportRepository.CreateReportWithCarsAsync(report, cars, ct);
            LogHelper.Info($"Rapor kaydedildi: '{dto.ReportName}' (Id={id}, {cars.Count} araç).");
            return id;
        }

        public async Task<int> OverwriteReportAsync(string reportName, CreateReportDto dto, CancellationToken ct = default)
        {
            var existing = await _reportRepository.GetByNameAsync(reportName, ct);
            if (existing != null)
            {
                await _reportRepository.DeleteAsync(existing.Id, ct);
                LogHelper.Info($"Üzerine yazma: eski rapor silindi '{reportName}' (Id={existing.Id}).");
            }
            return await SaveReportAsync(dto, ct);
        }

        public async Task<bool> ReportNameExistsAsync(string reportName, CancellationToken ct = default)
        {
            var existing = await _reportRepository.GetByNameAsync(reportName, ct);
            return existing != null;
        }

        public async Task<List<ResultReportDto>> GetAllReportsAsync(CancellationToken ct = default)
        {
            var reports = await _reportRepository.GetAllAsync(ct);
            var counts = await _carResultRepository.GetCountsByReportAsync(ct);

            return reports
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ResultReportDto
                {
                    Id = r.Id,
                    ReportName = r.ReportName,
                    PickupLocation = r.PickupLocation,
                    PickupDateTime = r.PickupDateTime,
                    ReturnDateTime = r.ReturnDateTime,
                    CreatedAt = r.CreatedAt,
                    CarCount = counts.TryGetValue(r.Id, out var c) ? c : 0
                })
                .ToList();
        }

        public async Task<List<ResultCarDto>> GetReportCarsAsync(int reportId, CancellationToken ct = default)
        {
            var cars = await _carResultRepository.GetByReportIdAsync(reportId, ct);
            return _carResultService.ToDtos(cars);
        }

        public async Task DeleteReportAsync(int reportId, CancellationToken ct = default)
        {
            await _reportRepository.DeleteAsync(reportId, ct);
            LogHelper.Info($"Rapor silindi (Id={reportId}).");
        }
    }
}
