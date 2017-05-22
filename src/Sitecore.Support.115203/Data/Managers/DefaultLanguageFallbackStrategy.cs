using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sitecore.Data;
using Sitecore.Data.Engines.DataCommands;
using Sitecore.Data.Events;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Diagnostics;
using Sitecore.Globalization;

namespace Sitecore.Support.Data.Managers
{
    /// <summary>
    /// Strategy to determine the fallback language for the specified one
    /// </summary>
    public class DefaultLanguageFallbackStrategy : LanguageFallbackStrategy
    {
        /// <summary>
        /// Represents language mapping for a single database
        /// </summary>
        protected class LanguageMapping
        {
            /// <summary>
            /// Internal table of the languages.
            /// </summary>
            private System.Collections.Generic.Dictionary<string, Language> languages;

            private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Language>>
                dependentLanguagesMapping =
                    new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Language>>();

            private object syncLock = new object();


            private static readonly System.Collections.Generic.List<Language> EmptyList =
                new System.Collections.Generic.List<Language>();

            /// <summary>
            /// Gets the fallback language.
            /// </summary>
            /// <param name="language">The language.</param>
            /// <returns></returns>
            public virtual Language GetFallbackLanguage(Language language)
            {
                Assert.ArgumentNotNull(language, "language");
                if (this.languages == null)
                {
                    return null;
                }
                Language result;
                if (this.languages.TryGetValue(language.Name.ToLowerInvariant(), out result))
                {
                    return result;
                }
                return null;
            }

            /// <summary>
            /// Gets the dependent languages.
            /// </summary>
            /// <param name="language">The language.</param>
            /// <returns></returns>
            public virtual System.Collections.Generic.List<Language> GetDependentLanguages(Language language)
            {
                Assert.ArgumentNotNull(language, "language");
                if (this.dependentLanguagesMapping == null)
                {
                    return DefaultLanguageFallbackStrategy.LanguageMapping.EmptyList;
                }
                System.Collections.Generic.List<Language> result;
                if (this.dependentLanguagesMapping.TryGetValue(language.Name.ToLowerInvariant(), out result))
                {
                    return result;
                }
                return DefaultLanguageFallbackStrategy.LanguageMapping.EmptyList;
            }

            /// <summary>
            /// Loads the specified database.
            /// </summary>
            /// <param name="database">The database.</param>
            public virtual void Load(Database database)
            {
                Assert.ArgumentNotNull(database, "database");
                Language[] array = database.Languages;
                System.Collections.Generic.Dictionary<string, Language> dictionary =
                    array.ToDictionary((Language language) => language.Name.ToLowerInvariant());
                System.Collections.Generic.Dictionary<string, Language> dictionary2 =
                    new System.Collections.Generic.Dictionary<string, Language>();
                Language[] array2 = array;
                for (int i = 0; i < array2.Length; i++)
                {
                    Language language2 = array2[i];
                    if (!language2.Origin.ItemId.IsNull)
                    {
                        Item item = database.GetItem(language2.Origin.ItemId);
                        Language value;
                        if (item != null &&
                            dictionary.TryGetValue(item[FieldIDs.FallbackLanguage].ToLowerInvariant(), out value))
                        {
                            dictionary2[item.Name.ToLowerInvariant()] = value;
                        }
                    }
                }
                if (this.languages == null)
                {
                    this.AttachEvents(database);
                }
                this.languages = dictionary2;
                this.LoadDependentLanguages(array);
            }

            /// <summary>
            /// Attaches the events.
            /// </summary>
            /// <param name="database">The database.</param>
            private void AttachEvents(Database database)
            {
                Assert.ArgumentNotNull(database, "database");
                string name = database.Name;
                database.Engines.DataEngine.SavedItem += (sender, e) => this.OnSavedItem(e.Command.Item);
                database.Engines.DataEngine.SavedItemRemote += (sender, e) => this.OnSavedItem(e.Item);
                Database.InstanceCreated += delegate(object source, InstanceCreatedEventArgs args)
                {
                    if (args.Database.Name == name)
                    {
                        args.Database.Engines.DataEngine.SavedItem += (sender, e) => this.OnSavedItem(e.Command.Item);
                        args.Database.Engines.DataEngine.SavedItemRemote += (sender, e) => this.OnSavedItem(e.Item);
                    }
                };
            }

            private void OnSavedItem([NotNull] Item item)
            {
                if (item.TemplateID == TemplateIDs.Language)
                {
                    lock (this.syncLock)
                    {
                        this.Load(item.Database);
                    }
                }
            }

            /// <summary>
            /// Handles the SavedItem event of the DataEngine control.
            /// </summary>
            /// <param name="sender">The source of the event.</param>
            /// <param name="e">The <see cref="T:Sitecore.Data.Events.ExecutedEventArgs`1" /> instance containing the event data.</param>
            private void DataEngine_SavedItem(object sender, ExecutedEventArgs<SaveItemCommand> e)
            {
                Assert.ArgumentNotNull(sender, "sender");
                Assert.ArgumentNotNull(e, "e");
                if (e.Command.Item.TemplateID == TemplateIDs.Language)
                {
                    this.Load(e.Command.Item.Database);
                }
            }

            private void LoadDependentLanguages(System.Collections.Generic.IEnumerable<Language> databaseLanguages)
            {
                this.dependentLanguagesMapping.Clear();
                foreach (string current in (from language in this.languages.Values
                    select language.Name.ToLowerInvariant()).Distinct<string>())
                {
                    System.Collections.Generic.List<Language> value =
                        (from s in this.CalculateDependentLanguages(current, null)
                            select databaseLanguages.First(
                                (Language language) => language.Name.ToLowerInvariant() == s))
                        .ToList<Language>();
                    this.dependentLanguagesMapping.Add(current, value);
                }
            }

            private System.Collections.Generic.IEnumerable<string> CalculateDependentLanguages(string languageName,
                HashSet<string> processedLanguages = null)
            {
                if (processedLanguages == null)
                {
                    processedLanguages = new HashSet<string>();
                }
                System.Collections.Generic.List<string> list = (from pair in this.languages
                    where pair.Value.Name.ToLowerInvariant() == languageName && processedLanguages.Add(pair.Key)
                    select pair.Key).ToList<string>();
                System.Collections.Generic.List<string> second =
                    list.SelectMany((string s) => this.CalculateDependentLanguages(s, processedLanguages))
                        .ToList<string>();
                return list.Concat(second);
            }
        }

        private readonly System.Collections.Generic.Dictionary<string, DefaultLanguageFallbackStrategy.LanguageMapping>
            mappings =
                new System.Collections.Generic.Dictionary<string, DefaultLanguageFallbackStrategy.LanguageMapping>();

        /// <summary>
        /// Gets the fallback language.
        /// </summary>
        /// <param name="language">The language for which to get the fallback.</param>
        /// <param name="database">The database which defines the fallback policy.</param>
        /// <param name="relatedItemId">ID of the related item</param>
        /// <returns>An instance of <see cref="T:Sitecore.Globalization.Language" /> class which is the fallback of the specified language in the specified database.</returns>
        public override Language GetFallbackLanguage(Language language, Database database, ID relatedItemId)
        {
            if (string.IsNullOrEmpty(language.Name))
            {
                return null;
            }
            return this.GetMapping(database).GetFallbackLanguage(language);
        }

        /// <summary>
        /// Gets the languages that depends on <paramref name="fallbackLanguage" />.
        /// </summary>
        /// <param name="fallbackLanguage">The fallback language.</param>
        /// <param name="database">The database.</param>
        /// <param name="relatedItemId">The related item identifier.</param>
        /// <returns></returns>
        public override System.Collections.Generic.List<Language> GetDependentLanguages(Language fallbackLanguage,
            Database database, ID relatedItemId)
        {
            if (string.IsNullOrEmpty(fallbackLanguage.Name))
            {
                return new System.Collections.Generic.List<Language>();
            }
            return this.GetMapping(database).GetDependentLanguages(fallbackLanguage);
        }

        /// <summary>
        /// Creates the language mapping.
        /// </summary>
        /// <returns></returns>
        protected virtual DefaultLanguageFallbackStrategy.LanguageMapping CreateLanguageMapping()
        {
            return new DefaultLanguageFallbackStrategy.LanguageMapping();
        }

        /// <summary>
        /// Gets the mapping for the specified database.
        /// </summary>
        /// <param name="database">The database for which to load mapping.</param>
        /// <returns>And instance of <see cref="T:Sitecore.Data.Managers.DefaultLanguageFallbackStrategy.LanguageMapping" /> class.</returns>
        protected DefaultLanguageFallbackStrategy.LanguageMapping GetMapping(Database database)
        {
            Assert.ArgumentNotNull(database, "database");
            DefaultLanguageFallbackStrategy.LanguageMapping languageMapping;
            lock (this)
            {
                if (this.mappings.TryGetValue(database.Name, out languageMapping))
                {
                    return languageMapping;
                }
                languageMapping = this.CreateLanguageMapping();
                this.mappings[database.Name] = languageMapping;
                languageMapping.Load(database);
            }
            return languageMapping;
        }
    }
}
