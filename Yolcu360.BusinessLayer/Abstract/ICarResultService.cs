using Yolcu360.DtoLayer.CarResultDto;
using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.BusinessLayer.Abstract
{
    public interface ICarResultService
    {
        /// <summary>DTO listesini entity listesine çevirir (kayıt öncesi).</summary>
        List<CarResult> ToEntities(List<ResultCarDto> dtos);

        /// <summary>Entity listesini DTO listesine çevirir (DataGridView için).</summary>
        List<ResultCarDto> ToDtos(List<CarResult> entities);
    }
}
