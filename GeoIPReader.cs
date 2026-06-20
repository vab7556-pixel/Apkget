using System;
using System.IO;
using System.Net;

namespace TcpServerApp
{
    /// <summary>
    /// قارئ بسيط لملفات GeoIP Legacy (.dat)
    /// يدعم قواعد بيانات MaxMind GeoIP Legacy Country
    /// </summary>
    public class GeoIPReader : IDisposable
    {
        private readonly byte[]? _data;
        private readonly bool _isLoaded;
        
        private const int COUNTRY_BEGIN = 16776960;
        private const int RECORD_LENGTH = 3;

        private static readonly string[] CountryCodes = {
            "--", "AP", "EU", "AD", "AE", "AF", "AG", "AI", "AL", "AM", "CW",
            "AO", "AQ", "AR", "AS", "AT", "AU", "AW", "AZ", "BA", "BB",
            "BD", "BE", "BF", "BG", "BH", "BI", "BJ", "BM", "BN", "BO",
            "BR", "BS", "BT", "BV", "BW", "BY", "BZ", "CA", "CC", "CD",
            "CF", "CG", "CH", "CI", "CK", "CL", "CM", "CN", "CO", "CR",
            "CU", "CV", "CX", "CY", "CZ", "DE", "DJ", "DK", "DM", "DO",
            "DZ", "EC", "EE", "EG", "EH", "ER", "ES", "ET", "FI", "FJ",
            "FK", "FM", "FO", "FR", "SX", "GA", "GB", "GD", "GE", "GF",
            "GH", "GI", "GL", "GM", "GN", "GP", "GQ", "GR", "GS", "GT",
            "GU", "GW", "GY", "HK", "HM", "HN", "HR", "HT", "HU", "ID",
            "IE", "IL", "IN", "IO", "IQ", "IR", "IS", "IT", "JM", "JO",
            "JP", "KE", "KG", "KH", "KI", "KM", "KN", "KP", "KR", "KW",
            "KY", "KZ", "LA", "LB", "LC", "LI", "LK", "LR", "LS", "LT",
            "LU", "LV", "LY", "MA", "MC", "MD", "MG", "MH", "MK", "ML",
            "MM", "MN", "MO", "MP", "MQ", "MR", "MS", "MT", "MU", "MV",
            "MW", "MX", "MY", "MZ", "NA", "NC", "NE", "NF", "NG", "NI",
            "NL", "NO", "NP", "NR", "NU", "NZ", "OM", "PA", "PE", "PF",
            "PG", "PH", "PK", "PL", "PM", "PN", "PR", "PS", "PT", "PW",
            "PY", "QA", "RE", "RO", "RU", "RW", "SA", "SB", "SC", "SD",
            "SE", "SG", "SH", "SI", "SJ", "SK", "SL", "SM", "SN", "SO",
            "SR", "ST", "SV", "SY", "SZ", "TC", "TD", "TF", "TG", "TH",
            "TJ", "TK", "TM", "TN", "TO", "TL", "TR", "TT", "TV", "TW",
            "TZ", "UA", "UG", "UM", "US", "UY", "UZ", "VA", "VC", "VE",
            "VG", "VI", "VN", "VU", "WF", "WS", "YE", "YT", "RS", "ZA",
            "ZM", "ME", "ZW", "A1", "A2", "O1", "AX", "GG", "IM", "JE",
            "BL", "MF", "BQ", "SS", "O1"
        };

        public GeoIPReader(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    _data = File.ReadAllBytes(filePath);
                    _isLoaded = _data.Length > 0;
                }
            }
            catch
            {
                _isLoaded = false;
            }
        }

        private static readonly string[] CountryNames = {
            "N/A", "Asia/Pacific Region", "Europe", "Andorra", "United Arab Emirates", "Afghanistan", "Antigua and Barbuda", "Anguilla", "Albania", "Armenia", "Netherlands Antilles", 
            "Angola", "Antarctica", "Argentina", "American Samoa", "Austria", "Australia", "Aruba", "Azerbaijan", "Bosnia and Herzegovina", "Barbados", 
            "Bangladesh", "Belgium", "Burkina Faso", "Bulgaria", "Bahrain", "Burundi", "Benin", "Bermuda", "Brunei Darussalam", "Bolivia", 
            "Brazil", "Bahamas", "Bhutan", "Bouvet Island", "Botswana", "Belarus", "Belize", "Canada", "Cocos (Keeling) Islands", "Congo, The Democratic Republic of the", 
            "Central African Republic", "Congo", "Switzerland", "Cote D'Ivoire", "Cook Islands", "Chile", "Cameroon", "China", "Colombia", "Costa Rica", 
            "Cuba", "Cape Verde", "Christmas Island", "Cyprus", "Czech Republic", "Germany", "Djibouti", "Denmark", "Dominica", "Dominican Republic", 
            "Algeria", "Ecuador", "Estonia", "Egypt", "Western Sahara", "Eritrea", "Spain", "Ethiopia", "Finland", "Fiji", 
            "Falkland Islands (Malvinas)", "Micronesia, Federated States of", "Faroe Islands", "France", "France, Metropolitan", "Gabon", "United Kingdom", "Grenada", "Georgia", "French Guiana", 
            "Ghana", "Gibraltar", "Greenland", "Gambia", "Guinea", "Guadeloupe", "Equatorial Guinea", "Greece", "South Georgia and the South Sandwich Islands", "Guatemala", 
            "Guam", "Guinea-Bissau", "Guyana", "Hong Kong", "Heard Island and McDonald Islands", "Honduras", "Croatia", "Haiti", "Hungary", "Indonesia", 
            "Ireland", "Israel", "India", "British Indian Ocean Territory", "Iraq", "Iran, Islamic Republic of", "Iceland", "Italy", "Jamaica", "Jordan", 
            "Japan", "Kenya", "Kyrgyzstan", "Cambodia", "Kiribati", "Comoros", "Saint Kitts and Nevis", "Korea, Democratic People's Republic of", "Korea, Republic of", "Kuwait", 
            "Cayman Islands", "Kazakstan", "Lao People's Democratic Republic", "Lebanon", "Saint Lucia", "Liechtenstein", "Sri Lanka", "Liberia", "Lesotho", "Lithuania", 
            "Luxembourg", "Latvia", "Libyan Arab Jamahiriya", "Morocco", "Monaco", "Moldova, Republic of", "Madagascar", "Marshall Islands", "Macedonia, the Former Yugoslav Republic of", "Mali", 
            "Myanmar", "Mongolia", "Macao", "Northern Mariana Islands", "Martinique", "Mauritania", "Montserrat", "Malta", "Mauritius", "Maldives", 
            "Malawi", "Mexico", "Malaysia", "Mozambique", "Namibia", "New Caledonia", "Niger", "Norfolk Island", "Nigeria", "Nicaragua", 
            "Netherlands", "Norway", "Nepal", "Nauru", "Niue", "New Zealand", "Oman", "Panama", "Peru", "French Polynesia", 
            "Papua New Guinea", "Philippines", "Pakistan", "Poland", "Saint Pierre and Miquelon", "Pitcairn", "Puerto Rico", "Palestinian Territory, Occupied", "Portugal", "Palau", 
            "Paraguay", "Qatar", "Reunion", "Romania", "Russian Federation", "Rwanda", "Saudi Arabia", "Solomon Islands", "Seychelles", "Sudan", 
            "Sweden", "Singapore", "Saint Helena", "Slovenia", "Svalbard and Jan Mayen", "Slovakia", "Sierra Leone", "San Marino", "Senegal", "Somalia", 
            "Suriname", "Sao Tome and Principe", "El Salvador", "Syrian Arab Republic", "Swaziland", "Turks and Caicos Islands", "Chad", "French Southern Territories", "Togo", 
            "Thailand", "Tajikistan", "Tokelau", "Turkmenistan", "Tunisia", "Tonga", "Timor-Leste", "Turkey", "Trinidad and Tobago", "Tuvalu", 
            "Taiwan, Province of China", "Tanzania, United Republic of", "Ukraine", "Uganda", "United States Minor Outlying Islands", "United States", "Uruguay", "Uzbekistan", "Holy See (Vatican City State)", 
            "Saint Vincent and the Grenadines", "Venezuela", "Virgin Islands, British", "Virgin Islands, U.S.", "Vietnam", "Vanuatu", "Wallis and Futuna", "Samoa", "Yemen", 
            "Mayotte", "Yugoslavia", "South Africa", "Zambia", "Montenegro", "Zimbabwe", "Anonymous Proxy", "Satellite Provider", "Other", "Aland Islands", 
            "Guernsey", "Isle of Man", "Jersey", "Saint Barthelemy", "Saint Martin"
        };

        public string GetCountryCode(string ipAddress)
        {
            var index = GetCountryIndex(ipAddress);
            if (index >= 0 && index < CountryCodes.Length)
                return CountryCodes[index];
            return "--";
        }

        public string GetCountryName(string ipAddress)
        {
            var index = GetCountryIndex(ipAddress);
            if (index >= 0 && index < CountryNames.Length)
                return CountryNames[index];
            return "Unknown";
        }

        private int GetCountryIndex(string ipAddress)
        {
            if (!_isLoaded || _data == null)
                return -1;

            try
            {
                if (!IPAddress.TryParse(ipAddress, out var ip))
                    return -1;

                // تحويل IP إلى رقم
                var bytes = ip.GetAddressBytes();
                if (bytes.Length != 4) return -1; // IPv4 فقط

                long ipNum = ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | 
                             ((long)bytes[2] << 8) | bytes[3];

                // البحث في قاعدة البيانات
                int offset = 0;
                for (int depth = 31; depth >= 0; depth--)
                {
                    int recordOffset = offset * 2 * RECORD_LENGTH;
                    if (recordOffset + 6 > _data.Length) return -1;

                    int x0 = (_data[recordOffset] & 0xFF) |
                             ((_data[recordOffset + 1] & 0xFF) << 8) |
                             ((_data[recordOffset + 2] & 0xFF) << 16);

                    int x1 = (_data[recordOffset + 3] & 0xFF) |
                             ((_data[recordOffset + 4] & 0xFF) << 8) |
                             ((_data[recordOffset + 5] & 0xFF) << 16);

                    offset = ((ipNum >> depth) & 1) == 1 ? x1 : x0;

                    if (offset >= COUNTRY_BEGIN)
                    {
                        return offset - COUNTRY_BEGIN;
                    }
                }
            }
            catch
            {
                // تجاهل الأخطاء
            }

            return -1;
        }

        public void Dispose()
        {
            // لا شيء للتنظيف
        }
    }
}
