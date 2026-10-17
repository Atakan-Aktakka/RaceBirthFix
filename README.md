# Race Birth Fix

**A crash fix for Realm of Thrones (ROT) in Mount & Blade II: Bannerlord.**

Fixes the campaign crash that happens on a child's birthday when the mother is human and the father has been turned into a White Walker or a wight.

[**⬇ Download from GitHub Releases**](https://github.com/Atakan-Aktakka/RaceBirthFix/releases/latest) · [**Nexus Mods page**](https://www.nexusmods.com/mountandblade2bannerlord/mods/14078)

*[Türkçe açıklama aşağıda / Turkish below](#türkçe)*

---

## Do I have this bug?

You probably do if all of these are true:

- You are playing a ROT campaign.
- The game crashes at the **same in-game day every time**, usually during the daily tick while time is passing on the map.
- Loading older saves does not help, because they all crash on that day too.
- Shortly before that, a married lord was converted to a White Walker or a wight. This can happen when a White Walker clan captures him, when a White Walker player executes him, or in a duel.

The Bannerlord crash dialog says *"The application faced a problem. We need to collect necessary files to fix this problem."* The crash comes from `HeroCreator.DeliverOffSpring` (HeroCreator.cs:275).

## What causes it

ROT turns some lords into White Walkers (`Race = 1`) or wights (`Race = 2`). When it does, it clears the lord's spouse, but it **does not update a pregnancy that is already running**. The pregnancy saved in the game still lists the converted lord as the father.

When the due date comes, Bannerlord tries to create a child from a human mother and a White Walker father:

1. The native check `mother.Race == father.Race` fails and the game crashes.
2. Even if that check is skipped, a male child gets a template from the wrong race or sex. This causes a native access violation (`0xC0000005`) while the hero is being created.

The pregnancy lives in the save file, so every save made after the conversion crashes on that day.

## What this mod does

The mod only does something when the **parents are of different races**. Normal births are not touched.

| | |
|---|---|
| ✅ | Skips the race check in `DeliverOffSpring` that crashes the game. |
| ✅ | Delivers the child as a **girl of the mother's race**, using the mother's template. This is the vanilla code path, so template, sex and race stay consistent. |
| ✅ | Generates the child's face only from the mother's body data, so two races' face data are never mixed. |
| ✅ | Shows a yellow `[RaceBirthFix]` message in game when it steps in. |
| ✅ | Writes each step of the birth to `Modules/RaceBirthFix/RaceBirthFix_log.txt`, so a crash can be traced if something else goes wrong. |

The mod does **not** write anything into your save. You can add it to a campaign that is already running, and remove it again later. If you remove it, a birth with mixed-race parents will crash again.

## Installation

1. Install [Harmony](https://www.nexusmods.com/mountandblade2bannerlord/mods/2006) if you don't already have it. ROT needs it too.
2. Download `RaceBirthFix-v1.0.0.zip` from [Releases](https://github.com/Atakan-Aktakka/RaceBirthFix/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/mountandblade2bannerlord/mods/14078).
3. Extract it into `Mount & Blade II Bannerlord/Modules/`. You should end up with `Modules/RaceBirthFix/SubModule.xml`.
4. Enable **Race Birth Fix** in the launcher. Load order does not matter as long as it comes after Harmony.
5. Load your save and let time pass. When the birth happens, the yellow `[RaceBirthFix]` message appears and the game keeps running.

> **Steam / Windows note:** if the game does not load the mod, right-click `bin/Win64_Shipping_Client/RaceBirthFix.dll`, open **Properties** and tick **Unblock**.

## Compatibility

| | Version |
|---|---|
| Bannerlord | v1.3.15 |
| Realm of Thrones | v1.3.15.3 |
| Harmony | v2.4.2+ |

The mod should keep working on nearby versions, as long as `HeroCreator.DeliverOffSpring` and `DefaultHeroCreationModel` do not change. It does not depend on ROT's code, so it is harmless in other mods or vanilla.

## Building from source

Requires the .NET SDK. The project targets .NET Framework 4.7.2.

```
cd src
dotnet build -c Release -p:GameDir="C:\path\to\Mount & Blade II Bannerlord" -p:HarmonyBin="C:\path\to\folder\with\0Harmony.dll"
```

The DLL is written to `bin/Win64_Shipping_Client/RaceBirthFix.dll`.

## Reporting problems

If the game still crashes on a birth with this mod enabled, please [open an issue](https://github.com/Atakan-Aktakka/RaceBirthFix/issues) and attach:

- `Modules/RaceBirthFix/RaceBirthFix_log.txt`. The last line shows the step where the crash happened.
- Your mod list and versions.

---

## Türkçe

**Realm of Thrones (ROT) için doğum çökmesi düzeltmesi.**

### Sorun

ROT bazı lordları White Walker'a (ırk 1) ya da wight'a (ırk 2) çeviriyor. Bu, White Walker klanı tarafından esir alınınca, White Walker oyuncu tarafından idam edilince ya da düelloda olabiliyor. ROT bu sırada lordun eşliğini siliyor, ama **devam eden hamileliğin babasını güncellemiyor**.

Doğum günü gelince anne insan, baba White Walker oluyor:

1. Oyun `HeroCreator.cs:275`'teki ırk kontrolüne takılıp çöküyor.
2. Bu kontrol atlansa bile erkek çocuğa yanlış ırk ya da cinsiyetten şablon verildiği için oyun yerel bir bellek hatasıyla (`0xC0000005`) çöküyor.

Hamilelik kayıt dosyasında tutulduğu için dönüşümden sonra alınan **bütün kayıtlar o gün çöküyor**.

### Mod ne yapıyor?

- Sadece anne ile babanın ırkı farklıysa devreye giriyor. Normal doğumlara dokunmuyor.
- Çökmeye yol açan ırk kontrolünü atlıyor.
- Çocuğu **annenin ırkından, annenin şablonuyla kız olarak** doğuruyor.
- Çocuğun yüzünü sadece annenin verisinden üretiyor.
- Doğumda sarı bir `[RaceBirthFix]` mesajı gösteriyor ve her adımı `RaceBirthFix_log.txt` dosyasına yazıyor.
- Kayıt dosyasına hiçbir şey yazmıyor. Devam eden kampanyaya eklenebilir.

### Kurulum

1. [Harmony](https://www.nexusmods.com/mountandblade2bannerlord/mods/2006) kurulu olmalı. ROT için zaten gerekli.
2. [Releases](https://github.com/Atakan-Aktakka/RaceBirthFix/releases/latest) ya da [Nexus Mods](https://www.nexusmods.com/mountandblade2bannerlord/mods/14078) sayfasından `RaceBirthFix-v1.0.0.zip` dosyasını indir.
3. Zip'i `Mount & Blade II Bannerlord/Modules/` klasörüne aç.
4. Launcher'da **Race Birth Fix**'i etkinleştir.
5. Kaydını yükle ve zamanı ilerlet. Doğumda sarı mesajı görürsen sorun çözülmüş demektir.

Hâlâ çöküyorsa `RaceBirthFix_log.txt` dosyasıyla birlikte bir [issue](https://github.com/Atakan-Aktakka/RaceBirthFix/issues) aç.

---

## License

[MIT](LICENSE) © 2026 Atakan Aktakka

*Not affiliated with TaleWorlds Entertainment or the Realm of Thrones team.*
