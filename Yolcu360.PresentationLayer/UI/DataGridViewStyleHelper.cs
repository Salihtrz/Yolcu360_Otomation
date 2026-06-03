using System.Drawing;
using System.Windows.Forms;

namespace Yolcu360.PresentationLayer.UI
{
    /// <summary>
    /// DataGridView'e modern, temiz bir tablo görünümü uygular. Yalnızca görsel ayarlar
    /// (veri/kolon mantığına dokunmaz).
    /// </summary>
    public static class DataGridViewStyleHelper
    {
        public static void Apply(DataGridView dgv)
        {
            dgv.EnableHeadersVisualStyles = false;
            dgv.BorderStyle = BorderStyle.None;
            dgv.BackgroundColor = ThemeColors.Surface;
            dgv.GridColor = ThemeColors.Border;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.RowHeadersVisible = false;
            dgv.AllowUserToResizeRows = false;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgv.ColumnHeadersHeight = 42;
            dgv.RowTemplate.Height = 34;
            dgv.Font = new Font("Segoe UI", 9.25f);

            // Başlık
            dgv.ColumnHeadersDefaultCellStyle.BackColor = ThemeColors.GridHeader;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemeColors.GridHeader;

            // Hücreler
            dgv.DefaultCellStyle.BackColor = ThemeColors.Surface;
            dgv.DefaultCellStyle.ForeColor = ThemeColors.TextDark;
            dgv.DefaultCellStyle.SelectionBackColor = ThemeColors.Primary;
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv.DefaultCellStyle.Padding = new Padding(8, 0, 4, 0);
            dgv.AlternatingRowsDefaultCellStyle.BackColor = ThemeColors.GridAltRow;
            dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = ThemeColors.Primary;
            dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;

            dgv.RowsDefaultCellStyle.BackColor = ThemeColors.Surface;
        }
    }
}
