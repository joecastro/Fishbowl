
namespace Microsoft.Bing
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.ServiceModel;
    using Microsoft.Bing.MicrosoftTranslator;
    using Standard;

    public class TranslationService
    {
        private readonly string _AppId;
        private readonly LanguageService _languageService;
        private readonly Dictionary<CultureInfo, string> _availableLanguages;

        public TranslationService(string appId)
        {
            Verify.IsNeitherNullNorEmpty(appId, "appId");
            _AppId = appId;

            var binding = new BasicHttpBinding();
            var endpoint = new EndpointAddress("http://api.microsofttranslator.com/v1/Soap.svc");
            _languageService = new LanguageServiceClient(binding, endpoint);
            string[] languages = Utility.FailableFunction(() => _languageService.GetLanguages(_AppId));
            _availableLanguages = languages.ToDictionary(lan => new CultureInfo(lan));
        }

        public IEnumerable<CultureInfo> GetAvailableLanguages()
        {
            return _availableLanguages.Keys;
        }

        public CultureInfo Detect(string text)
        {
            Verify.IsNeitherNullNorEmpty(text, "text");

            string language = _languageService.Detect(_AppId, text);
            return new CultureInfo(language);
        }

        public string Translate(string text, CultureInfo to)
        {
            CultureInfo fromCultureInfo = Detect(text);
            if (!_availableLanguages.ContainsKey(fromCultureInfo))
            {
                // Don't do an implicit translation if Bing doesn't recognize the language.
                return text;
            }
            return Translate(text, fromCultureInfo, to);
        }

        public string Translate(string text, CultureInfo from, CultureInfo to)
        {
            Verify.IsNeitherNullNorEmpty(text, "text");
            Verify.IsNotNull(from, "from");
            Verify.IsNotNull(to, "to");

            if (from.Equals(to))
            {
                return text;
            }

            string fromString;
            string toString;
            if (!_availableLanguages.TryGetValue(from, out fromString))
            {
                throw new ArgumentException("Language is not supported.", "from");
            }

            if (!_availableLanguages.TryGetValue(to, out toString))
            {
                throw new ArgumentException("Language is not supported.", "to");
            }

            return _languageService.Translate(_AppId, text, fromString, toString);
        }
    }
}
