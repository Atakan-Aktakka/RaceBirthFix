using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using FaceGen = TaleWorlds.Core.FaceGen;

namespace RaceBirthFix
{
    // ROT (Realm of Thrones) lordlari White Walker'a cevirirken (Race = 1/2) esligi siliyor ama devam eden
    // hamileligin babasini degistirmiyor. Dogum gunu gelince anne insan, baba White Walker oluyor ve
    // 1) HeroCreator.DeliverOffSpring icindeki SilentAssert(mother.Race == father.Race) oyunu cokertiyor,
    // 2) erkek cocuga babanin (farkli irk) sablonu ya da annenin (kadin) sablonu verilince native cokme oluyor.
    // Bu mod assert'i yutar, cocugu kiz olarak annenin sablonu ve irkiyla dogurur, yuzu sadece annenin verisinden uretir.
    public class SubModule : MBSubModuleBase
    {
        private const string HarmonyId = "racebirthfix.patch";
        private static Harmony _harmony;
        private static bool _modelPatched;
        private static string _logPath;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                _logPath = Path.Combine(Path.GetDirectoryName(typeof(SubModule).Assembly.Location), "..", "..", "RaceBirthFix_log.txt");
                Log("---- oyun basladi ----");
                _harmony = new Harmony(HarmonyId);

                foreach (var name in new[] { "SilentAssert", "Assert" })
                {
                    var target = AccessTools.GetDeclaredMethods(typeof(MBDebugManager))
                        .FirstOrDefault(m => m.Name.EndsWith("." + name));
                    if (target != null)
                    {
                        _harmony.Patch(target, prefix: new HarmonyMethod(typeof(SubModule), name + "Prefix"));
                        Log(name + " yamasi uygulandi: " + target.Name);
                    }
                    else
                    {
                        Log("UYARI: MBDebugManager." + name + " bulunamadi");
                    }
                }

                var deliver = AccessTools.Method(typeof(HeroCreator), nameof(HeroCreator.DeliverOffSpring));
                _harmony.Patch(deliver,
                    prefix: new HarmonyMethod(typeof(SubModule), nameof(DeliverPrefix)),
                    postfix: new HarmonyMethod(typeof(SubModule), nameof(DeliverPostfix)),
                    finalizer: new HarmonyMethod(typeof(SubModule), nameof(DeliverFinalizer)));
                Log("DeliverOffSpring yamasi uygulandi");
            }
            catch (Exception e)
            {
                Log("OnSubModuleLoad hata: " + e);
            }
        }

        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            try
            {
                if (_modelPatched || !(game.GameType is Campaign))
                    return;

                var model = Campaign.Current?.Models?.HeroCreationModel;
                if (model == null)
                {
                    Log("UYARI: HeroCreationModel yok");
                    return;
                }
                Log("HeroCreationModel tipi: " + model.GetType().FullName);

                var template = AccessTools.Method(model.GetType(), "GetCharacterTemplateForOffspring");
                if (template != null)
                    _harmony.Patch(template, postfix: new HarmonyMethod(typeof(SubModule), nameof(TemplatePostfix)));

                var body = AccessTools.Method(model.GetType(), "GetStaticBodyProperties");
                if (body != null)
                    _harmony.Patch(body, prefix: new HarmonyMethod(typeof(SubModule), nameof(BodyPrefix)));

                foreach (var name in new[] { "GetBornSettlement", "GetPreferredUpgradeFormation", "GetClan", "GetCulture", "GenerateFirstAndFullName",
                    "GetTraitsForHero", "GetDefaultSkillsForHero", "GetCivilianEquipment", "GetBattleEquipment" })
                    PatchStep(AccessTools.Method(model.GetType(), name));
                PatchStep(AccessTools.Method(typeof(CampaignEventDispatcher), "OnHeroCreated"));
                PatchStep(AccessTools.Method(typeof(CampaignEventDispatcher), "OnGivenBirth"));

                _modelPatched = true;
                Log($"Model yamalari: template={template != null}, body={body != null}");
            }
            catch (Exception e)
            {
                Log("OnGameInitializationFinished hata: " + e);
            }
        }

        // Farkli irktan dogum surerken patlayan her assert'i yut ve nerede patladigini logla.
        [ThreadStatic] private static bool _mixedBirth;

        // Erkek cocuk vanilla'da babanin sablonunu alir; annenin (kadin) sablonunu erkek cocuga vermek yerine
        // farkli irktan dogumlarda cocugu kiz yapiyoruz ki sablon ve cinsiyet tutarli kalsin.
        private static void DeliverPrefix(Hero mother, Hero father, ref bool isOffspringFemale)
        {
            _mixedBirth = RacesDiffer(mother, father);
            if (_mixedBirth)
            {
                _tracing = true;
                Log($"Farkli irktan dogum basliyor (kiz cocuk, orijinal secim: {(isOffspringFemale ? "kiz" : "erkek")})");
                isOffspringFemale = true;
            }
        }

        private static void DeliverPostfix(Hero __result)
        {
            if (_mixedBirth)
                Log("Dogum tamamlandi: " + (__result?.Name?.ToString() ?? "?"));
            _mixedBirth = false;
        }

        private static Exception DeliverFinalizer(Exception __exception)
        {
            if (__exception != null)
                Log("DeliverOffSpring hata: " + __exception);
            _mixedBirth = false;
            return __exception;
        }

        // Native cokme olursa logdaki son satir hangi adimda patladigini gosterir.
        [ThreadStatic] private static bool _tracing;

        private static void PatchStep(System.Reflection.MethodBase method)
        {
            if (method == null)
                return;
            _harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(SubModule), nameof(StepPrefix)),
                postfix: new HarmonyMethod(typeof(SubModule), nameof(StepPostfix)));
        }

        private static void StepPrefix(System.Reflection.MethodBase __originalMethod)
        {
            if (_tracing)
                Log("  > " + __originalMethod.Name);
        }

        private static void StepPostfix(System.Reflection.MethodBase __originalMethod)
        {
            if (!_tracing)
                return;
            Log("  < " + __originalMethod.Name);
            if (__originalMethod.Name == "OnGivenBirth")
            {
                _tracing = false;
                Log("Dogum olaylari bitti");
            }
        }

        private static bool SilentAssertPrefix(bool condition, string message, string callerFile, string callerMethod, int callerLine)
        {
            return HandleAssert("SilentAssert", condition, message, callerFile, callerMethod, callerLine);
        }

        private static bool AssertPrefix(bool condition, string message, string callerFile, string callerMethod, int callerLine)
        {
            return HandleAssert("Assert", condition, message, callerFile, callerMethod, callerLine);
        }

        private static bool HandleAssert(string kind, bool condition, string message, string callerFile, string callerMethod, int callerLine)
        {
            if (condition || !(_mixedBirth || callerMethod == "DeliverOffSpring"))
                return true;

            Log($"{kind} atlandi: {Path.GetFileName(callerFile)}:{callerLine} {callerMethod} '{message}'{Environment.NewLine}{Environment.StackTrace}");
            return false;
        }

        private static bool RacesDiffer(Hero mother, Hero father)
        {
            return mother?.CharacterObject != null && father?.CharacterObject != null
                && mother.CharacterObject.Race != father.CharacterObject.Race;
        }

        private static void TemplatePostfix(Hero mother, Hero father, ref CharacterObject __result)
        {
            try
            {
                if (!RacesDiffer(mother, father))
                    return;

                __result = mother.CharacterObject;
                var msg = $"[RaceBirthFix] {mother.Name} (irk {mother.CharacterObject.Race}) ve {father.Name} (irk {father.CharacterObject.Race}) farkli irktan; cocuk annenin irkindan doguyor.";
                Log(msg);
                InformationManager.DisplayMessage(new InformationMessage(msg, Colors.Yellow));
            }
            catch (Exception e)
            {
                Log("TemplatePostfix hata: " + e);
            }
        }

        // Vanilla offspring yolunun kopyasi; tek fark baba yerine yine annenin vucut verisi ve etiketleri kullaniliyor.
        private static bool BodyPrefix(Hero hero, bool isOffspring, float variationAmount, ref StaticBodyProperties __result)
        {
            try
            {
                if (!isOffspring || hero == null || !RacesDiffer(hero.Mother, hero.Father))
                    return true;

                var mother = hero.Mother;
                int race = mother.CharacterObject.Race;
                var range = mother.CharacterObject.BodyPropertyRange;
                string hairTags = range?.HairTags ?? "";
                string beardTags = range?.BeardTags ?? "";
                string tattooTags = range?.TattooTags ?? "";

                BodyProperties props = BodyProperties.GetRandomBodyProperties(race, hero.IsFemale, mother.BodyProperties, mother.BodyProperties,
                    1, MBRandom.RandomInt(), hairTags, beardTags, tattooTags, variationAmount);

                int hair = -1, beard = -1, tattoo = -1;
                var bodyModel = Campaign.Current.Models.BodyPropertiesModel;
                int gender = hero.IsFemale ? 1 : 0;
                if (string.IsNullOrEmpty(hairTags))
                {
                    int[] idx = bodyModel.GetHairIndicesForCulture(race, gender, hero.Age, hero.Culture);
                    hair = idx.Length != 0 ? idx.GetRandomElement() : -1;
                }
                if (string.IsNullOrEmpty(beardTags))
                {
                    int[] idx = bodyModel.GetBeardIndicesForCulture(race, gender, hero.Age, hero.Culture);
                    beard = idx.Length != 0 ? idx.GetRandomElement() : -1;
                }
                if (string.IsNullOrEmpty(tattooTags))
                {
                    int[] idx = bodyModel.GetTattooIndicesForCulture(race, gender, hero.Age, hero.Culture);
                    tattoo = idx.Length != 0 ? idx.GetRandomElement() : -1;
                    if (MBRandom.RandomFloat < FaceGen.GetTattooZeroProbability(race, gender, hero.Age))
                        tattoo = 0;
                }
                FaceGen.SetHair(ref props, hair, beard, tattoo);
                __result = props.StaticProperties;
                Log($"Cocuk yuzu annenin irkindan uretildi (irk {race})");
                return false;
            }
            catch (Exception e)
            {
                Log("BodyPrefix hata (vanilla'ya donuluyor): " + e);
                return true;
            }
        }

        private static void Log(string text)
        {
            try
            {
                Debug.Print("[RaceBirthFix] " + text);
                if (_logPath != null)
                    File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            }
            catch
            {
            }
        }
    }
}
