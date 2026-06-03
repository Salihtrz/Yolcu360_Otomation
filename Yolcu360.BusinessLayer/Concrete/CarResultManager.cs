using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.DtoLayer.CarResultDto;
using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.BusinessLayer.Concrete
{
    /// <summary>
    /// Araç sonuçları için DTO &lt;-&gt; Entity dönüşümlerini yapar.
    /// </summary>
    public class CarResultManager : ICarResultService
    {
        public List<CarResult> ToEntities(List<ResultCarDto> dtos)
        {
            if (dtos == null) return new List<CarResult>();
            return dtos.Select(d => new CarResult
            {
                CarModel = d.CarModel,
                RentalCompany = d.RentalCompany,
                TransmissionType = d.TransmissionType,
                FuelType = d.FuelType,
                Segment = d.Segment,
                Price = d.Price,
                Currency = d.Currency,
                PickupLocation = d.PickupLocation,
                PickupDateTime = d.PickupDateTime,
                ReturnDateTime = d.ReturnDateTime,
                SourceUrl = d.SourceUrl,
                ImageUrl = d.ImageUrl,
                ScrapedAt = d.ScrapedAt
            }).ToList();
        }

        public List<ResultCarDto> ToDtos(List<CarResult> entities)
        {
            if (entities == null) return new List<ResultCarDto>();
            return entities.Select(e => new ResultCarDto
            {
                CarModel = e.CarModel,
                RentalCompany = e.RentalCompany,
                TransmissionType = e.TransmissionType,
                FuelType = e.FuelType,
                Segment = e.Segment,
                Price = e.Price,
                Currency = e.Currency,
                PickupLocation = e.PickupLocation,
                PickupDateTime = e.PickupDateTime,
                ReturnDateTime = e.ReturnDateTime,
                SourceUrl = e.SourceUrl,
                ImageUrl = e.ImageUrl,
                ScrapedAt = e.ScrapedAt
            }).ToList();
        }
    }
}
