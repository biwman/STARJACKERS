# New gadgets and illegal upgrades

Stan: wdrozone w katalogu itemow, craftingu, blueprintach, runtime gadgetow, VFX, ikonach i audio.

## Balans i dzialanie

| Item | Typ | Cena kupna | Sprzedaz | Ladunki | Dzialanie | Salvage |
| --- | --- | ---: | ---: | ---: | --- | --- |
| Loot Hook | Gadget, Rare | 6800 | 1800 | 2 | Zasieg 5.8. Kradnie najcenniejszy niestrzezony cargo item z najblizszego wrogiego statku bez zabijania. Ignoruje SAFE pocket i slot astronauty, wymaga wolnego slotu u zlodzieja. | Tractor Beam, Asteroid Gold |
| Stasis Buoy | Gadget, Rare | 6400 | 1700 | 2 | Deployable na 8.5 s. Puls EMP co 0.58 s w promieniu 4.25, predkosc celu x0.38, fire interval x1.75. Ma 55 HP i 25 shield. | Space Mine Wreck, Asteroid Rare |
| Tether Harpoon | Gadget, Epic | 9300 | 2500 | 2 | Zasieg 7.2. Lapie najblizszy wrogi statek na 4.5 s, ciagnie cel do dystansu napiecia i lekko przyciaga wlasciciela. Co 0.35 s naklada krotki shock. | Tractor Beam, Droid Scrap, Asteroid Rare |
| Space Torpedo | Gadget, Rare | 8500 | 2200 | 2 | Szybki pocisk z nosa statku. Predkosc 9.4, lifetime 4.1 s, wybuch 2.6, 95 dmg, 150 obstacle dmg. | Rocket Launcher, Asteroid Gold, Space Junk |
| Bio Trap | Gadget, Epic | 10000 | 2600 | 1 | Zasieg 4.6. Lapie najblizszego wrogiego astronauta i zamienia go w Captive Astronaut Pod jako loot. | Space Trap, Rescue Ship Salvage, Asteroid Rare |
| Asteroid Breacher Bomb | Gadget, Rare | 7200 | 1900 | 2 | Zasieg celu 6.4. Detonuje najblizsza asteroide-obstacle, zadaje 85 dmg w promieniu 3.25 i niszczy obstacle, jezeli room pozwala na destructible obstacles. | Space Mine Wreck, Space Junk, Asteroid Rare |
| Overclocked Magazine | Support, Rare | 5900 | 1500 | pasywne | Nielegalne ulepszenie: +60% max ammo, ale reload x1.25 i shield statku = 0. Single-install. | Droid Scrap, Space Junk, Asteroid Gold |
| Black Market Thruster | Engine, Epic | 10500 | 2600 | pasywne | Nielegalne ulepszenie: +28% speed bonus, +15 boost percent, -8% turn rate i -35 max shield. Single-install. | Fusion Engine, Pirate Fighter Salvage, Asteroid Rare |
| Captive Astronaut Pod | Resource, Rare | brak | 2600 | loot | Kontrabanda z Bio Trap. Sprzedawalny loot, salvage zwraca rescue/asteroid komponenty. | Rescue Ship Salvage, Asteroid Gold |

## Wdrozenie

1. `InventoryItemCatalog` dodaje stale itemow, definicje ekonomii, salvage, ikony, blueprint definitions oraz efekty nielegalnych ulepszen na shield.
2. `PlayerShooting` dodaje ladunki gadgetow, kolory UI, Overclocked Magazine dla ammo/reload, transakcje Loot Hook i RPC dla VFX/audio.
3. `NewItemsRuntime` dodaje deployable markery, Stasis Buoy, Space Torpedo, Bio Trap capture, Tether Harpoon, Asteroid Breacher i lekkie runtime VFX.
4. `PlayerMovement` dodaje Black Market Thruster do profilu silnika.
5. `PlayerHealth` liczy shield przez `GetConfiguredMaxShield`, z pelnym disable dla Overclocked Magazine i kara -35 dla Black Market Thruster.
6. `PlayerProfileCrafting` dodaje receptury dla nowych blueprintow.
7. `BlueprintCatalog` dodaje oferty Miss Enigma, scrap offers i tematyczne dropy blueprintow z wrakow.
8. `WeaponAttackCatalog` dodaje klasyfikacje Ion/Gravitic/Explosive/Environmental dla nowych gadgetow.
9. Assety:
   - Ikony: `Assets/Resources/Items/*.png`
   - Blueprinty: `Assets/Resources/Items/Blueprints/*_blueprint.png`
   - Audio: `Assets/Resources/Audio/loot_hook_snatch.wav`, `stasis_buoy_deploy.wav`, `stasis_pulse.wav`, `tether_harpoon_fire.wav`, `bio_trap_capture.wav`, `asteroid_breacher_blast.wav`, `space_torpedo_launch.wav`, `space_torpedo_explosion.wav`
   - Concept reference: `tmp/imagegen/new_gadgets_concept_sheet.png`

## Test checklist

- Loot Hook: w dwoch klientach sprawdzic kradziez najcenniejszego cargo, brak kradziezy SAFE/ASTRO i rollback, gdy slot u zlodzieja jest pelny.
- Stasis Buoy: sprawdzic, czy cel w aura radius ma wolniejszy ruch i wolniejszy fire/reload przez caly czas pulsu.
- Tether Harpoon: sprawdzic, czy harpun nie lapie sojusznika/wlasciciela i czy konczy efekt po zniknieciu celu.
- Space Torpedo: trafienie w statek, deployable, obstacle i lifetime timeout.
- Bio Trap: zlapac wrogiego astronauta, podniesc Captive Astronaut Pod i sprzedac.
- Asteroid Breacher Bomb: odpalic przy obstacle i przy braku obstacle.
- Illegal upgrades: zalozyc Overclocked Magazine i Black Market Thruster, sprawdzic UI ammo, reload, shield i handling.
