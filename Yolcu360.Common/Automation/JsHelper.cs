using Newtonsoft.Json;

namespace Yolcu360.Common.Automation
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

        // ====================== SAAT SEÇİMİ (gerçek DOM, 2026) ======================
        // Sayfada tam 2 saat tetikleyicisi vardır: cursor:pointer, metni tam "HH:MM" olan DIV'ler
        // (index 0 = Alış, 1 = Dönüş). Tıklayınca açılan menü body'ye taşınır ve seçenekler
        //   <li class="... select-none list-none cursor-pointer">HH:MM</li>  (48 adet, 30 dk)
        // şeklindedir. Menü YALNIZCA gerçek (CDP/trusted) tıklama ile açılır; JS .click() açmaz.
        // Bu yüzden tetikleyiciler index ile bulunur, koordinatları CDP gerçek tıklamayla kullanılır.

        /// <summary>Sayfadaki saat tetikleyicilerini (cursor:pointer, metni "HH:MM" DIV/SPAN/BUTTON) DOM sırasıyla toplar; LI seçenekleri hariç tutar.</summary>
        private const string TimeTriggerCollect = @"
            var __nodes = document.querySelectorAll('div, span, button');
            var __trig = [];
            for (var __i = 0; __i < __nodes.length; __i++) {
                var __n = __nodes[__i];
                if (__n.tagName === 'LI') continue;
                var __t = (__n.textContent || '').trim();
                if (/^\d{1,2}:\d{2}$/.test(__t)) {
                    try { if (getComputedStyle(__n).cursor === 'pointer') __trig.push(__n); } catch (e) {}
                }
            }";

        /// <summary>index. saat tetikleyicisinin viewport merkez koordinatını JSON ("{x,y}") döndürür (CDP tıklama için). Yoksa "".</summary>
        public static string BuildGetTimeTriggerRectByIndexScript(int index)
        {
            return $@"(function(){{
                {TimeTriggerCollect}
                if (__trig.length <= {index}) return '';
                var el = __trig[{index}];
                try {{ el.scrollIntoView({{ block: 'center', inline: 'center' }}); }} catch (e) {{}}
                var r = el.getBoundingClientRect();
                return JSON.stringify({{ x: r.left + r.width / 2, y: r.top + r.height / 2 }});
            }})();";
        }

        /// <summary>index. saat tetikleyicisinin O AN gösterdiği değeri ("HH:MM") döndürür; doğrulama için. Yoksa "".</summary>
        public static string BuildGetDisplayedTimeByIndexScript(int index)
        {
            return $@"(function(){{
                {TimeTriggerCollect}
                if (__trig.length <= {index}) return '';
                return (__trig[{index}].textContent || '').trim();
            }})();";
        }

        /// <summary>Açık saat menüsü var mı? (metni HH:MM olan en az 10 li → menü açık).</summary>
        public static string BuildIsTimeMenuOpenScript()
        {
            return @"(function(){
                var lis = document.querySelectorAll('li');
                var c = 0;
                for (var i = 0; i < lis.length; i++) {
                    if (/^\d{1,2}:\d{2}$/.test((lis[i].textContent || '').trim())) c++;
                }
                return c >= 10;
            })();";
        }

        /// <summary>
        /// Saat seçici tetikleyicisinin (etiketin yanındaki HH:MM gösteren cursor:pointer div)
        /// VIEWPORT merkez koordinatını JSON ("{x,y}") döndürür; gerçek (CDP) fare tıklaması için.
        /// Öğe görünür alana kaydırılır. Bulunamazsa "" döner.
        /// </summary>
        public static string BuildGetTimeTriggerRectScript(string labelText)
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
                                    try {{
                                        if (getComputedStyle(ds[j]).cursor === 'pointer') {{
                                            ds[j].scrollIntoView({{ block: 'center', inline: 'center' }});
                                            var r = ds[j].getBoundingClientRect();
                                            return JSON.stringify({{ x: r.left + r.width / 2, y: r.top + r.height / 2 }});
                                        }}
                                    }} catch (e) {{}}
                                }}
                            }}
                            c = c.parentElement;
                        }}
                    }}
                }}
                return '';
            }})();";
        }

        /// <summary>
        /// Açık saat listesinde metni hedefe (ör. "15:00") eşit GÖRÜNÜR li.hour-li öğesinin
        /// viewport merkez koordinatını JSON ("{x,y}") döndürür. Liste içinde görünür alana kaydırılır.
        /// Bulunamazsa "" döner.
        /// </summary>
        public static string BuildGetHourOptionRectScript(string target)
        {
            return $@"(function(){{
                var target = {Json(target)};
                var lis = document.querySelectorAll('li');
                for (var i = 0; i < lis.length; i++) {{
                    if ((lis[i].textContent || '').trim() === target) {{
                        try {{ lis[i].scrollIntoView({{ block: 'center' }}); }} catch (e) {{}}
                        var r = lis[i].getBoundingClientRect();
                        if (r.width > 0 && r.height > 0)
                            return JSON.stringify({{ x: r.left + r.width / 2, y: r.top + r.height / 2 }});
                    }}
                }}
                return '';
            }})();";
        }

        /// <summary>
        /// Saat seçicinin O AN GÖSTERDİĞİ değeri (etiketin yanındaki cursor:pointer HH:MM div'i)
        /// döndürür; seçim sonrası doğrulama (readback) için. Bulunamazsa "" döner.
        /// </summary>
        public static string BuildGetDisplayedTimeScript(string labelText)
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
                                    try {{ if (getComputedStyle(ds[j]).cursor === 'pointer') return t; }} catch (e) {{}}
                                }}
                            }}
                            c = c.parentElement;
                        }}
                    }}
                }}
                return '';
            }})();";
        }

        /// <summary>
        /// Açık saat listesinde metni hedefe eşit li.hour-li öğesini görünür alana kaydırıp
        /// .click() (untrusted) ile seçmeyi dener. Liste açıkken li @click çoğunlukla yeterlidir.
        /// </summary>
        public static string BuildClickHourOptionJsScript(string target)
        {
            return $@"(function(){{
                var target = {Json(target)};
                var lis = document.querySelectorAll('li');
                for (var i = 0; i < lis.length; i++) {{
                    if ((lis[i].textContent || '').trim() === target) {{
                        try {{ lis[i].scrollIntoView({{ block: 'center' }}); }} catch (e) {{}}
                        try {{ lis[i].click(); return true; }} catch (e) {{}}
                    }}
                }}
                return false;
            }})();";
        }

        /// <summary>Açık saat menüsünün kaydırılabilir kapsayıcısını bir adım aşağı kaydırır (hedef alttaysa görünür olsun).</summary>
        public static string BuildScrollHourListScript()
        {
            return @"(function(){
                var lis = document.querySelectorAll('li');
                var hourLi = null;
                for (var i = 0; i < lis.length; i++) {
                    if (/^\d{1,2}:\d{2}$/.test((lis[i].textContent || '').trim())) { hourLi = lis[i]; break; }
                }
                if (!hourLi) return false;
                var el = hourLi.parentElement;
                for (var up = 0; up < 6 && el; up++) {
                    if (el.scrollHeight > el.clientHeight + 5) {
                        el.scrollTop = Math.min(el.scrollTop + 130, el.scrollHeight);
                        return true;
                    }
                    el = el.parentElement;
                }
                return true;
            })();";
        }

        /// <summary>
        /// Lokasyon autocomplete önerilerinden, yazılan metne EN İYİ eşleşeni tıklar (ilkini değil).
        /// Türkçe karakterler normalize edilir; yazılan metnin tokenleri öneri metninde aranır,
        /// tam içerme bonuslu. Hiç eşleşme yoksa ilk öneriye düşer. true/false döner.
        /// </summary>
        public static string BuildClickBestLocationSuggestionScript(string[] selectors, string typed)
        {
            return $@"(function(){{
                var sels = {Json(selectors)};
                function norm(s){{ return (s||'').toLowerCase()
                    .replace(/ı/g,'i').replace(/İ/g,'i').replace(/ş/g,'s').replace(/Ş/g,'s')
                    .replace(/ğ/g,'g').replace(/Ğ/g,'g').replace(/ü/g,'u').replace(/Ü/g,'u')
                    .replace(/ö/g,'o').replace(/Ö/g,'o').replace(/ç/g,'c').replace(/Ç/g,'c'); }}
                var tn = norm({Json(typed)});
                var tokens = tn.split(/[^a-z0-9]+/).filter(function(w){{ return w.length >= 3; }});
                var items = [];
                for (var i = 0; i < sels.length; i++) {{
                    try {{ var n = document.querySelectorAll(sels[i]); if (n.length) {{ items = Array.prototype.slice.call(n); break; }} }} catch (e) {{}}
                }}
                if (!items.length) return false;
                var best = null, bestScore = -1;
                for (var j = 0; j < items.length; j++) {{
                    var txt = norm(items[j].innerText || items[j].textContent || '');
                    var score = 0;
                    for (var k = 0; k < tokens.length; k++) {{ if (txt.indexOf(tokens[k]) >= 0) score++; }}
                    if (tn && txt.indexOf(tn) >= 0) score += 5;
                    if (score > bestScore) {{ bestScore = score; best = items[j]; }}
                }}
                try {{ (best && bestScore > 0 ? best : items[0]).click(); return true; }} catch (e) {{ return false; }}
            }})();";
        }

        /// <summary>
        /// Sayfanın localStorage'ındaki TÜM anahtar/değerleri JSON nesnesi (string) olarak döndürür.
        /// Motorlar arası oturum köprüsü için (Yolcu360 auth token'ı çerezde değil localStorage'da olabilir).
        /// </summary>
        public static string BuildGetLocalStorageScript()
        {
            return @"(function(){
                try {
                    var o = {};
                    for (var i = 0; i < localStorage.length; i++) {
                        var k = localStorage.key(i);
                        o[k] = localStorage.getItem(k);
                    }
                    return JSON.stringify(o);
                } catch (e) { return '{}'; }
            })();";
        }

        /// <summary>
        /// Verilen JSON nesnesindeki tüm anahtar/değerleri sayfanın localStorage'ına yazar
        /// (köprülenen oturum token'larını geri yüklemek için). true/false döner.
        /// </summary>
        public static string BuildSetLocalStorageScript(string localStorageJson)
        {
            return $@"(function(){{
                try {{
                    var data = JSON.parse({Json(string.IsNullOrWhiteSpace(localStorageJson) ? "{}" : localStorageJson)});
                    for (var k in data) {{
                        if (Object.prototype.hasOwnProperty.call(data, k)) localStorage.setItem(k, data[k]);
                    }}
                    return true;
                }} catch (e) {{ return false; }}
            }})();";
        }

        /// <summary>Verilen selector listesindeki ilk inputun O ANKİ value'sunu döndürür (lokasyon doğrulama). Yoksa "".</summary>
        public static string BuildGetInputValueScript(string[] selectors)
        {
            return $@"(function(){{
                {LibPreamble}
                var el = __y360_first({Json(selectors)});
                return el ? (el.value || '') : '';
            }})();";
        }

        /// <summary>
        /// TEŞHİS: Saat seçicinin gerçek DOM yapısını döndürür (JSON). Hangi selector'ların var
        /// olduğunu kör tahmin etmemek için: li.hour-li sayısı + örnek metinleri, görünür HH:MM
        /// cursor:pointer tetikleyici metinleri, &lt;select&gt; var mı, ve olası saat kapsayıcısının
        /// kırpılmış outerHTML'i. Logdan canlı DOM görülüp selector'lar kesinleştirilir.
        /// </summary>
        public static string BuildDumpTimeDomScript()
        {
            return @"(function(){
                var info = { hourLiCount: 0, hourSamples: [], triggers: [], selects: 0, selectSamples: [], html: '' };
                var lis = document.querySelectorAll('li.hour-li');
                info.hourLiCount = lis.length;
                for (var i = 0; i < lis.length && i < 8; i++) info.hourSamples.push((lis[i].textContent||'').trim());

                var divs = document.querySelectorAll('div, span, button');
                for (var j = 0; j < divs.length && info.triggers.length < 10; j++){
                    var t = (divs[j].textContent||'').trim();
                    if (/^\d{1,2}:\d{2}$/.test(t)){
                        var cur=''; try { cur = getComputedStyle(divs[j]).cursor; } catch(e){}
                        info.triggers.push(divs[j].tagName + '[' + (divs[j].className||'') + '] cur=' + cur + ' txt=' + t);
                    }
                }

                var sels = document.querySelectorAll('select');
                info.selects = sels.length;
                for (var k = 0; k < sels.length && k < 4; k++){
                    var opts = sels[k].querySelectorAll('option');
                    var sample = [];
                    for (var m = 0; m < opts.length && m < 4; m++) sample.push((opts[m].textContent||'').trim());
                    info.selectSamples.push('name=' + (sels[k].name||'') + ' id=' + (sels[k].id||'') + ' opts=' + sample.join('|'));
                }

                var cont = null;
                if (lis.length){ cont = lis[0]; for (var u=0; u<6 && cont.parentElement; u++) cont = cont.parentElement; }
                else { var mh = document.querySelector('.month-header'); if (mh){ cont = mh; for (var u2=0; u2<5 && cont.parentElement; u2++) cont = cont.parentElement; } }
                if (cont) info.html = (cont.outerHTML || '').replace(/\s+/g,' ').slice(0, 4500);
                return JSON.stringify(info);
            })();";
        }

        /// <summary>
        /// TEŞHİS-2: Saat tetikleyicisi (etiketin yanındaki cursor:pointer HH:MM div) gerçek
        /// tıklandıktan SONRA, onun dropdown-kök atasının (class'ında 'relative' geçen) outerHTML'ini
        /// döndürür. Açılan menünün/saat seçeneklerinin gerçek DOM yapısını görmek için.
        /// </summary>
        public static string BuildDumpTimeDropdownScript(string labelText)
        {
            return $@"(function(){{
                var label = {Json(labelText)};
                var all = document.querySelectorAll('*');
                for (var i = 0; i < all.length; i++) {{
                    var n = all[i];
                    if (n.childNodes.length === 1 && (n.textContent || '').trim() === label) {{
                        var c = n.parentElement;
                        for (var up = 0; up < 4 && c; up++) {{
                            var ds = c.querySelectorAll('div');
                            for (var j = 0; j < ds.length; j++) {{
                                var t = (ds[j].textContent || '').trim();
                                if (/^\d{{1,2}}:\d{{2}}$/.test(t)) {{
                                    try {{
                                        if (getComputedStyle(ds[j]).cursor === 'pointer') {{
                                            var root = ds[j];
                                            for (var u = 0; u < 7 && root.parentElement; u++) {{
                                                root = root.parentElement;
                                                if ((root.className || '').indexOf('relative') >= 0) break;
                                            }}
                                            return (root.outerHTML || '').replace(/\s+/g, ' ').slice(0, 6000);
                                        }}
                                    }} catch (e) {{}}
                                }}
                            }}
                            c = c.parentElement;
                        }}
                    }}
                }}
                return 'TRIGGER_NOT_FOUND';
            }})();";
        }

        /// <summary>
        /// TEŞHİS-3: Açık saat menüsünü TÜM dökümanı tarayarak bulur (Vue Teleport/portal ile
        /// body'ye taşınmış olabilir). En çok HH:MM çocuğu içeren görünür kapsayıcıyı seçer ve
        /// yapısını (tag, class, çocuk tag/class, örnek metinler, kırpılmış HTML) JSON döndürür.
        /// Bu sayede saat seçeneklerinin gerçek selector'ı (li mi, div mi, hangi class) öğrenilir.
        /// </summary>
        public static string BuildDumpOpenTimeMenuScript()
        {
            return @"(function(){
                var all = document.querySelectorAll('ul, ol, div, [role=menu], [role=listbox]');
                var best = null, bestCount = 0;
                for (var i = 0; i < all.length; i++){
                    var el = all[i];
                    var kids = el.children;
                    var cnt = 0;
                    for (var j = 0; j < kids.length; j++){
                        var t = (kids[j].textContent || '').trim();
                        if (/^\d{1,2}:\d{2}$/.test(t)) cnt++;
                    }
                    if (cnt > bestCount){ bestCount = cnt; best = el; }
                }
                if (!best || bestCount < 2) return JSON.stringify({ found: false, count: bestCount });
                var info = { found: true, optCount: bestCount, tag: best.tagName, cls: best.className,
                             childCount: best.children.length,
                             childTag: best.children[0] ? best.children[0].tagName : '',
                             childCls: best.children[0] ? best.children[0].className : '',
                             samples: [] };
                for (var k = 0; k < best.children.length && k < 8; k++)
                    info.samples.push((best.children[k].textContent || '').trim());
                info.html = (best.outerHTML || '').replace(/\s+/g, ' ').slice(0, 2500);
                return JSON.stringify(info);
            })();";
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
        /// Bir radio grubunu (ör. 'filter-distance_limit.' / 'filter-provision.') tek seçimli
        /// olarak ayarlar. <paramref name="chosenId"/> verilmişse o radio seçilir (gerekirse tıklanır);
        /// diğer tüm grup radio'ları temizlenir. <paramref name="chosenId"/> boş/null ise grup tümüyle
        /// temizlenir (radio'lar tıkla ile kapanmadığından el.checked=false + change dispatch edilir).
        /// id'de nokta/artı/boşluk olabildiği için CSS yerine id.indexOf(prefix) ile eşleştirilir.
        /// </summary>
        public static string BuildSetRadioScript(string idPrefix, string chosenId)
        {
            return $@"(function(){{
                var prefix = {Json(idPrefix)};
                var chosen = {Json(chosenId ?? "")};
                document.querySelectorAll('input[type=radio]').forEach(function(el){{
                    if (el.id && el.id.indexOf(prefix) === 0) {{
                        if (chosen && el.id === chosen) {{
                            if (!el.checked) el.click();
                        }} else if (el.checked) {{
                            el.checked = false;
                            el.dispatchEvent(new Event('change', {{ bubbles: true }}));
                        }}
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

        /// <summary>
        /// Sonuç sayfasındaki filtre panelini (#stickyFilterCardContent) dinamik okur.
        /// Her &lt;details data-cms-key&gt; bölümü için başlık + tüm filter-* seçeneklerini
        /// (id, etiket "(adet)" ayıklanmış, input tipi) JSON dizisi olarak döndürür.
        /// Böylece marka/şirket/model gibi listeler uygulamada SABİT tutulmaz, siteden gelir.
        /// </summary>
        public static string BuildScrapeFiltersScript()
        {
            return @"(function(){
                var root = document.querySelector('#stickyFilterCardContent');
                if (!root) return JSON.stringify([]);
                var out = [];
                root.querySelectorAll('details').forEach(function(d){
                    var key = d.getAttribute('data-cms-key') || '';
                    var t = d.querySelector('summary .font-semibold');
                    var title = t ? (t.innerText || t.textContent || '').trim() : key;
                    var seen = {};
                    var opts = [];
                    d.querySelectorAll('input[id^=""filter-""]').forEach(function(inp){
                        var id = inp.id;
                        if (!id || seen[id]) return; seen[id] = 1;
                        var lbl = inp.closest('label') || inp.parentElement;
                        var txt = lbl ? (lbl.innerText || lbl.textContent || '').trim() : '';
                        txt = txt.replace(/\s*\(\d+\)\s*$/, '').trim();
                        opts.push({ id: id, label: txt, type: inp.type });
                    });
                    if (opts.length > 0)
                        out.push({ key: key, title: title, type: opts[0].type, options: opts });
                });
                return JSON.stringify(out);
            })();";
        }

        /// <summary>
        /// Sitenin gösterebildiği bilgi/uyarı modalını (ör. saat dilimi uyarısı, çerez "Kabul Et")
        /// best-effort kapatır: metni Tamam/Kapat/Kabul Et/Anladım olan görünür buton/öğeye tıklar.
        /// true = bir şey tıklandı.
        /// </summary>
        public static string BuildDismissModalScript()
        {
            return @"(function(){
                var words = ['Tamam','Kapat','Kabul Et','Anladım','Anladim','Devam','Reddet'];
                var nodes = document.querySelectorAll(""button, a, span, div, [role='button']"");
                for (var i = 0; i < nodes.length; i++){
                    var el = nodes[i];
                    var txt = (el.innerText || el.textContent || '').trim();
                    for (var j = 0; j < words.length; j++){
                        if (txt === words[j]){
                            try { if (el.offsetParent !== null) { el.click(); return true; } } catch(e){}
                        }
                    }
                }
                return false;
            })();";
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

        /// <summary>Mevcut sonuç kartı sayısını (string) döndürür; lazy-load'da "hepsi yüklendi mi" kontrolü için.</summary>
        public static string BuildCountResultsScript(string[] cardSelectors)
        {
            return $@"(function(){{
                {LibPreamble}
                var sels = {Json(cardSelectors)};
                for (var i=0;i<sels.length;i++){{
                    try {{ var n=document.querySelectorAll(sels[i]); if (n.length>0) return String(n.length); }} catch(e){{}}
                }}
                return '0';
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

        // ============================================================================
        //  FİRMA DEĞERLENDİRME / MİSAFİR YORUMU OKUMA (salt-okunur)
        //  Siteye yorum gönderilmez; yalnızca zaten görünen veriler okunur.
        // ============================================================================

        /// <summary>
        /// Verilen firma logo UUID'sine sahip İLK araç kartını bulur, "Yorum" tetikleyicisini
        /// görünür alana kaydırır ve merkez koordinatını (CSS px) döner. Trusted (CDP) tıklama için.
        /// JSON: { found:bool, x:number, y:number }.
        /// </summary>
        public static string BuildFindReviewTriggerScript(string supplierUuid, string[] cardSelectors, string[] logoSelectors, string[] triggerSelectors)
        {
            return $@"(function(){{
                {LibPreamble}
                var uuid = {Json(supplierUuid)};
                var cardSels = {Json(cardSelectors)};
                var logoSels = {Json(logoSelectors)};
                var trigSels = {Json(triggerSelectors)};
                var cards = [];
                for (var i=0;i<cardSels.length;i++){{ try{{ var f=document.querySelectorAll(cardSels[i]); if(f.length>0){{ cards=Array.prototype.slice.call(f); break; }} }}catch(e){{}} }}
                var target=null;
                for (var c=0;c<cards.length;c++){{
                    var logo=__y360_firstIn(cards[c], logoSels);
                    var src=logo?(logo.getAttribute('src')||logo.getAttribute('data-src')||''):'';
                    if (uuid && src.indexOf(uuid)>=0){{ target=cards[c]; break; }}
                }}
                if (!target) return JSON.stringify({{ found:false }});
                var trig=__y360_firstIn(target, trigSels);
                if (!trig) return JSON.stringify({{ found:false }});
                try {{ trig.scrollIntoView({{block:'center'}}); }} catch(e){{}}
                var r=trig.getBoundingClientRect();
                return JSON.stringify({{ found:true, x:(r.left+r.width/2), y:(r.top+r.height/2) }});
            }})();";
        }

        /// <summary>UUID eşleşen kartın "Yorum" tetikleyicisine JS click gönderir (CDP tıklama yedeği).</summary>
        public static string BuildClickReviewTriggerScript(string supplierUuid, string[] cardSelectors, string[] logoSelectors, string[] triggerSelectors)
        {
            return $@"(function(){{
                {LibPreamble}
                var uuid = {Json(supplierUuid)};
                var cardSels = {Json(cardSelectors)};
                var logoSels = {Json(logoSelectors)};
                var trigSels = {Json(triggerSelectors)};
                var cards = [];
                for (var i=0;i<cardSels.length;i++){{ try{{ var f=document.querySelectorAll(cardSels[i]); if(f.length>0){{ cards=Array.prototype.slice.call(f); break; }} }}catch(e){{}} }}
                for (var c=0;c<cards.length;c++){{
                    var logo=__y360_firstIn(cards[c], logoSels);
                    var src=logo?(logo.getAttribute('src')||logo.getAttribute('data-src')||''):'';
                    if (uuid && src.indexOf(uuid)>=0){{
                        var trig=__y360_firstIn(cards[c], trigSels);
                        if (trig){{ try{{ trig.click(); }}catch(e){{}} return true; }}
                    }}
                }}
                return false;
            }})();";
        }

        /// <summary>Değerlendirme modalı açık mı?</summary>
        public static string BuildIsReviewModalOpenScript(string[] modalSelectors)
        {
            return $@"(function(){{ {LibPreamble} return !!__y360_first({Json(modalSelectors)}); }})();";
        }

        /// <summary>Modaldaki kapatma (×) düğmesine tıklar.</summary>
        public static string BuildCloseReviewModalScript(string[] closeSelectors)
        {
            return $@"(function(){{
                {LibPreamble}
                var el=__y360_first({Json(closeSelectors)});
                if (el){{ try{{ el.click(); }}catch(e){{}} return true; }}
                return false;
            }})();";
        }

        /// <summary>
        /// Açık değerlendirme modalından firma özetini ve ilk <paramref name="maxReviews"/> yorumu okur.
        /// Alt puanlar yeşil ilerleme çubuğunun width:% değerinden (rating = %/20) çıkarılır.
        /// Yorum metinleri p.text-sm.font-semibold.text-black üzerinden alınır (kırılgan renk
        /// sınıflarına dokunmadan). JSON döner.
        /// </summary>
        public static string BuildScrapeReviewModalScript(string[] modalSelectors, int maxReviews)
        {
            return $@"(function(){{
                {LibPreamble}
                var modal = __y360_first({Json(modalSelectors)});
                if (!modal) return JSON.stringify({{ found:false }});

                function num(s){{ if(!s) return 0; var m=String(s).replace(',','.').match(/-?\d+(\.\d+)?/); return m?parseFloat(m[0]):0; }}
                function ratingFromKey(key){{
                    var lbl = modal.querySelector('[data-cms-key=""'+key+'""]');
                    if(!lbl) return 0;
                    var box = lbl.parentElement || lbl;
                    var bar = box.querySelector('div[style*=""width""]');
                    if(bar){{ var m=(bar.getAttribute('style')||'').match(/width:\s*([\d.]+)%/); if(m) return Math.round(parseFloat(m[1])/20*10)/10; }}
                    var t=(box.innerText||''); var nm=t.match(/([\d]+[.,][\d]+)\s*$/); if(nm) return num(nm[1]);
                    return 0;
                }}

                var supplierName=''; var loc='';
                var snEl = modal.querySelector('.text-base .text-steel.font-semibold'); if(snEl) supplierName=(snEl.innerText||'').trim();
                var locEl = modal.querySelector('.text-base .font-bold.text-dark-gray'); if(locEl) loc=(locEl.innerText||'').trim();

                var overall=0;
                var ovEl = modal.querySelector('.text-white.font-bold.rounded-md'); if(ovEl) overall=num(ovEl.innerText);

                var count=0;
                var all = modal.querySelectorAll('*');
                for(var i=0;i<all.length;i++){{
                    var el=all[i];
                    if(el.children.length===0){{
                        var tx=(el.textContent||'').trim();
                        if(/^\d[\d.]*\s*Yorum$/i.test(tx)){{ count=parseInt(tx.replace(/[^\d]/g,''),10)||0; break; }}
                    }}
                }}

                var reviews=[];
                var ps = modal.querySelectorAll('p.text-sm.font-semibold.text-black');
                for(var j=0;j<ps.length && reviews.length<{maxReviews};j++){{
                    var p=ps[j];
                    var comment=(p.innerText||'').trim();
                    if(!comment) continue;
                    var card=p.parentElement;
                    for(var k=0;k<3 && card && !card.querySelector('.w-12.rounded-full');k++){{ card=card.parentElement; }}
                    var author=''; var date=''; var rating=0;
                    if(card){{
                        var aEl=card.querySelector('.w-12.rounded-full'); if(aEl) author=(aEl.innerText||'').trim();
                        var dEl=card.querySelector('.text-sm.text-steel'); if(dEl) date=(dEl.innerText||'').trim();
                        var rEl=card.querySelector('.text-white.font-bold.rounded-md'); if(rEl) rating=num(rEl.innerText);
                    }}
                    reviews.push({{ author:author, date:date, rating:rating, comment:comment }});
                }}

                return JSON.stringify({{
                    found:true, supplierName:supplierName, location:loc,
                    overall:overall, count:count,
                    cleanliness:ratingFromKey('comment_rating_text1'),
                    delivery:ratingFromKey('comment_rating_text2'),
                    staff:ratingFromKey('comment_rating_text3'),
                    reviews:reviews
                }});
            }})();";
        }
    }
}
