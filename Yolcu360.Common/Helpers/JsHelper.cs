using Newtonsoft.Json;

namespace Yolcu360.Common.Helpers
{
    /// <summary>
    /// CefSharp içinde çalıştırılacak JavaScript kod parçalarını string olarak üretir.
    /// Tüm değerler JSON ile güvenli şekilde gömülür (enjeksiyon/kaçış sorununu önler).
    ///
    /// Modern web formları (React/Vue) basit <c>element.value = x</c> atamasını
    /// algılamaz; bu yüzden native value setter çağrılır ve input/change/blur
    /// olayları dispatch edilir (bkz. <see cref="LibPreamble"/>).
    /// </summary>
    public static class JsHelper
    {
        /// <summary>
        /// Her script'in başına eklenen yardımcı fonksiyonlar. Bağımsız (IIFE içinde)
        /// kullanılabilir.
        /// </summary>
        public const string LibPreamble = @"
function __y360_first(selectors){
    for (var i=0;i<selectors.length;i++){
        try { var el = document.querySelector(selectors[i]); if (el) return el; } catch(e){}
    }
    return null;
}
function __y360_firstIn(root, selectors){
    for (var i=0;i<selectors.length;i++){
        try { var el = root.querySelector(selectors[i]); if (el) return el; } catch(e){}
    }
    return null;
}
function __y360_setNativeValue(element, value){
    var valueSetter = Object.getOwnPropertyDescriptor(element, 'value');
    valueSetter = valueSetter ? valueSetter.set : undefined;
    var prototype = Object.getPrototypeOf(element);
    var protoDesc = Object.getOwnPropertyDescriptor(prototype, 'value');
    var prototypeValueSetter = protoDesc ? protoDesc.set : undefined;

    if (prototypeValueSetter && valueSetter !== prototypeValueSetter) {
        prototypeValueSetter.call(element, value);
    } else if (valueSetter) {
        valueSetter.call(element, value);
    } else {
        element.value = value;
    }
    element.dispatchEvent(new Event('input',  { bubbles: true }));
    element.dispatchEvent(new Event('change', { bubbles: true }));
    element.dispatchEvent(new Event('blur',   { bubbles: true }));
}
function __y360_text(el){ return el ? (el.innerText || el.textContent || '').trim() : ''; }
function __y360_val(el){
    if (!el) return '';
    if (el.tagName === 'IMG') return el.getAttribute('src') || el.getAttribute('data-src') || '';
    return (el.innerText || el.textContent || '').trim();
}
";

        private static string Json(object value) => JsonConvert.SerializeObject(value);

        /// <summary>Verilen selector listesindeki ilk inputa değer yazar. true/false döner.</summary>
        public static string BuildSetInputValueScript(string[] selectors, string value)
        {
            return $@"(function(){{
                {LibPreamble}
                var el = __y360_first({Json(selectors)});
                if (!el) return false;
                el.focus();
                __y360_setNativeValue(el, {Json(value)});
                return true;
            }})();";
        }

        /// <summary>
        /// Gerçek klavye yazımını taklit eder: her karakter için keydown + value güncelleme +
        /// input + keyup olayları dispatch eder. Modern (React/Vue) autocomplete'ler basit value
        /// atamasını algılamadığı için gereklidir. true/false döner.
        /// </summary>
        public static string BuildTypeRealScript(string[] selectors, string value)
        {
            return $@"(function(){{
                {LibPreamble}
                var el = __y360_first({Json(selectors)});
                if (!el) return false;
                var proto = Object.getPrototypeOf(el);
                var desc = Object.getOwnPropertyDescriptor(proto, 'value');
                var setter = desc ? desc.set : null;
                var text = {Json(value)};
                el.focus();
                if (setter) setter.call(el, ''); else el.value = '';
                for (var i = 0; i < text.length; i++) {{
                    var ch = text[i];
                    el.dispatchEvent(new KeyboardEvent('keydown', {{ key: ch, bubbles: true }}));
                    if (setter) setter.call(el, text.slice(0, i + 1)); else el.value = text.slice(0, i + 1);
                    el.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    el.dispatchEvent(new KeyboardEvent('keyup', {{ key: ch, bubbles: true }}));
                }}
                el.dispatchEvent(new Event('change', {{ bubbles: true }}));
                return true;
            }})();";
        }

        /// <summary>
        /// Takvimde belirli bir ayın (ör. "Haziran 2026") belirli gününe (ör. 10) tıklar.
        /// month-header metni eşleşen ayın grid'inde, metni güne eşit ve geçmiş/disabled
        /// olmayan (.before-day içermeyen) .day hücresini bulup tıklar. true/false döner.
        /// </summary>
        public static string BuildClickCalendarDayScript(string monthHeaderText, int day)
        {
            return $@"(function(){{
                var headers = document.querySelectorAll('.month-header');
                for (var i = 0; i < headers.length; i++) {{
                    if (headers[i].textContent.trim() === {Json(monthHeaderText)}) {{
                        var container = headers[i].parentElement;
                        var cells = container.querySelectorAll('.day');
                        for (var j = 0; j < cells.length; j++) {{
                            var c = cells[j];
                            if (c.textContent.trim() === {Json(day.ToString())} &&
                                c.className.indexOf('before-day') < 0) {{
                                c.click();
                                return true;
                            }}
                        }}
                    }}
                }}
                return false;
            }})();";
        }

        /// <summary>
        /// Saat seçicinin tetikleyicisine tıklar. Belirtilen etiketin (ör. "Alış Saati")
        /// yakınındaki, HH:MM gösteren tıklanabilir (cursor:pointer) div'i bulup tıklar;
        /// bu, altındaki saat listesini (li.hour-li) açar. true/false döner.
        /// </summary>
        public static string BuildClickTimeTriggerScript(string labelText)
        {
            return $@"(function(){{
                var label = {Json(labelText)};
                var all = document.querySelectorAll('*');
                for (var i = 0; i < all.length; i++) {{
                    var n = all[i];
                    if (n.childNodes.length === 1 && n.textContent.trim() === label) {{
                        var c = n.parentElement;
                        for (var up = 0; up < 4 && c; up++) {{
                            var ds = c.querySelectorAll('div');
                            for (var j = 0; j < ds.length; j++) {{
                                var t = ds[j].textContent.trim();
                                if (/^\d{{1,2}}:\d{{2}}$/.test(t)) {{
                                    try {{ if (getComputedStyle(ds[j]).cursor === 'pointer') {{ ds[j].click(); return true; }} }} catch (e) {{}}
                                }}
                            }}
                            c = c.parentElement;
                        }}
                    }}
                }}
                return false;
            }})();";
        }

        /// <summary>
        /// Açık saat listesinde metni hedef saate (ör. "08:00") eşit olan li.hour-li
        /// öğesine tıklar. Önce görünür olanı dener. true/false döner.
        /// </summary>
        public static string BuildClickTimeOptionScript(string target)
        {
            return $@"(function(){{
                var target = {Json(target)};
                var lis = document.querySelectorAll('li.hour-li');
                for (var i = 0; i < lis.length; i++) {{
                    if (lis[i].textContent.trim() === target && lis[i].offsetParent !== null) {{ lis[i].click(); return true; }}
                }}
                for (var k = 0; k < lis.length; k++) {{
                    if (lis[k].textContent.trim() === target) {{ lis[k].click(); return true; }}
                }}
                return false;
            }})();";
        }

        /// <summary>
        /// Bir checkbox'ı istenen duruma getirir: yalnızca mevcut durumu hedeften farklıysa
        /// tıklar (idempotent). Böylece zaten seçili filtreler tekrar uygulamada kapanmaz.
        /// Checkbox bulunamazsa false, bulunduysa true döner.
        /// </summary>
        public static string BuildSetCheckboxScript(string elementId, bool desired)
        {
            // getElementById, nokta içeren id'lerde de güvenlidir (CSS ayrıştırması yok).
            return $@"(function(){{
                var el = document.getElementById({Json(elementId)});
                if (!el) return false;
                if (el.checked !== {(desired ? "true" : "false")}) el.click();
                return true;
            }})();";
        }

        /// <summary>
        /// Bir önekle başlayan TÜM checkbox'ları (ör. 'filter-vendor.') istenmeyenleri kapatacak
        /// şekilde sıfırlar; yalnızca <paramref name="keepIds"/> içindekiler açık bırakılır.
        /// </summary>
        public static string BuildResetCheckboxGroupScript(string idPrefix, string[] keepIds)
        {
            return $@"(function(){{
                var keep = {Json(keepIds)};
                var prefix = {Json(idPrefix)};
                document.querySelectorAll('input[type=checkbox]').forEach(function(el){{
                    if (el.id && el.id.indexOf(prefix) === 0) {{
                        var want = keep.indexOf(el.id) >= 0;
                        if (el.checked !== want) el.click();
                    }}
                }});
                return true;
            }})();";
        }

        /// <summary>
        /// Verilen metinlerden biriyle TAM eşleşen görünür bir buton/bağlantı/öğeye tıklar.
        /// CSS selector'ı işe yaramadığında "Giriş Yap", "Devam Et" gibi metin tabanlı
        /// butonları bulmak için kullanılır. true/false döner.
        /// </summary>
        public static string BuildClickByTextScript(string[] texts)
        {
            return $@"(function(){{
                var wanted = {Json(texts)};
                var nodes = document.querySelectorAll(""button, a, span, div, [role='button'], input[type='button'], input[type='submit']"");
                for (var i = 0; i < nodes.length; i++) {{
                    var el = nodes[i];
                    var t = (el.innerText || el.textContent || el.value || '').trim();
                    for (var j = 0; j < wanted.length; j++) {{
                        if (t === wanted[j]) {{
                            try {{
                                if (el.offsetParent !== null || el.getClientRects().length > 0) {{ el.click(); return true; }}
                            }} catch (e) {{}}
                        }}
                    }}
                }}
                return false;
            }})();";
        }

        /// <summary>
        /// OTP kodunu 6 ayrı haneli inputa dağıtarak yazar. Selector listesinden, kod
        /// uzunluğu kadar input içeren ilk grup seçilir. Her hane için native value setter +
        /// keydown/input/keyup/change olayları dispatch edilir (modern formlar için). true/false döner.
        /// </summary>
        public static string BuildFillOtpDigitsScript(string[] selectors, string code)
        {
            return $@"(function(){{
                var code = {Json(code)};
                var sels = {Json(selectors)};
                var inputs = [];
                for (var s = 0; s < sels.length; s++) {{
                    try {{
                        var nodes = document.querySelectorAll(sels[s]);
                        if (nodes.length >= code.length) {{ inputs = Array.prototype.slice.call(nodes); break; }}
                    }} catch (e) {{}}
                }}
                if (inputs.length < code.length) return false;
                for (var i = 0; i < code.length; i++) {{
                    var el = inputs[i];
                    var ch = code[i];
                    var proto = Object.getPrototypeOf(el);
                    var desc = Object.getOwnPropertyDescriptor(proto, 'value');
                    var setter = desc ? desc.set : null;
                    el.focus();
                    el.dispatchEvent(new KeyboardEvent('keydown', {{ key: ch, bubbles: true }}));
                    if (setter) setter.call(el, ch); else el.value = ch;
                    el.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    el.dispatchEvent(new KeyboardEvent('keyup', {{ key: ch, bubbles: true }}));
                    el.dispatchEvent(new Event('change', {{ bubbles: true }}));
                }}
                return true;
            }})();";
        }

        /// <summary>İlk eşleşen elemana tıklar. true/false döner.</summary>
        public static string BuildClickScript(string[] selectors)
        {
            return $@"(function(){{
                {LibPreamble}
                var el = __y360_first({Json(selectors)});
                if (!el) return false;
                el.click();
                return true;
            }})();";
        }

        /// <summary>Eleman DOM'da var mı? true/false döner (bekleme döngüsü için).</summary>
        public static string BuildElementExistsScript(string[] selectors)
        {
            return $@"(function(){{
                {LibPreamble}
                return __y360_first({Json(selectors)}) != null;
            }})();";
        }

        /// <summary>En az bir sonuç kartı var mı? (sonuçların yüklenmesini beklemek için).</summary>
        public static string BuildHasResultsScript(string[] cardSelectors)
        {
            return $@"(function(){{
                {LibPreamble}
                var sels = {Json(cardSelectors)};
                for (var i=0;i<sels.length;i++){{
                    try {{ if (document.querySelectorAll(sels[i]).length > 0) return true; }} catch(e){{}}
                }}
                return false;
            }})();";
        }

        /// <summary>İlk eşleşen elemanın metnini döner; yoksa null.</summary>
        public static string BuildGetTextScript(string[] selectors)
        {
            return $@"(function(){{
                {LibPreamble}
                var el = __y360_first({Json(selectors)});
                return el ? __y360_text(el) : null;
            }})();";
        }

        /// <summary>Verilen selector listesindeki TÜM elemanların metinlerini dizi olarak döner.</summary>
        public static string BuildGetElementsTextScript(string[] selectors)
        {
            return $@"(function(){{
                {LibPreamble}
                var out = [];
                var sels = {Json(selectors)};
                for (var i=0;i<sels.length;i++){{
                    try {{
                        var nodes = document.querySelectorAll(sels[i]);
                        if (nodes.length>0){{
                            nodes.forEach(function(n){{ out.push(__y360_text(n)); }});
                            break;
                        }}
                    }} catch(e){{}}
                }}
                return JSON.stringify(out);
            }})();";
        }

        /// <summary>
        /// Sonuç sayfasındaki tüm araç kartlarını kazır ve JSON dizisi (string) döner.
        /// Her eleman: { carModel, rentalCompany, transmission, fuel, segment,
        /// priceText, imageUrl }. Fiyat ayrıştırması C# tarafında yapılır.
        /// </summary>
        public static string BuildScrapeResultsScript(
            string[] cardSelectors,
            string[] modelSelectors,
            string[] companySelectors,
            string[] transmissionSelectors,
            string[] fuelSelectors,
            string[] segmentSelectors,
            string[] priceSelectors,
            string[] imageSelectors)
        {
            return $@"(function(){{
                {LibPreamble}
                var cardSels = {Json(cardSelectors)};
                var cards = [];
                for (var i=0;i<cardSels.length;i++){{
                    try {{
                        var found = document.querySelectorAll(cardSels[i]);
                        if (found.length>0){{ cards = Array.prototype.slice.call(found); break; }}
                    }} catch(e){{}}
                }}

                var modelSels = {Json(modelSelectors)};
                var companySels = {Json(companySelectors)};
                var transSels = {Json(transmissionSelectors)};
                var fuelSels = {Json(fuelSelectors)};
                var segmentSels = {Json(segmentSelectors)};
                var priceSels = {Json(priceSelectors)};
                var imageSels = {Json(imageSelectors)};

                var result = [];
                cards.forEach(function(card){{
                    var img = __y360_firstIn(card, imageSels);
                    var imageUrl = '';
                    if (img) imageUrl = img.getAttribute('src') || img.getAttribute('data-src') || '';

                    result.push({{
                        carModel:      __y360_text(__y360_firstIn(card, modelSels)),
                        rentalCompany: __y360_val(__y360_firstIn(card, companySels)),
                        transmission:  __y360_text(__y360_firstIn(card, transSels)),
                        fuel:          __y360_text(__y360_firstIn(card, fuelSels)),
                        segment:       __y360_text(__y360_firstIn(card, segmentSels)),
                        priceText:     __y360_text(__y360_firstIn(card, priceSels)),
                        imageUrl:      imageUrl
                    }});
                }});
                return JSON.stringify(result);
            }})();";
        }
    }
}
