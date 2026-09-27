# Отложенные ошибки трейтов

Зафиксировано 2026-09-25 по просьбе пользователя. Это список для последующего
исправления; игровые файлы в рамках этой заметки не менялись.
Наблюдения подтверждены чтением прототипов и кода, без проверки в запущенной игре.

## Гурман: описание не соответствует компонентам

- Трейт: `MedievalFood`.
- В русской локализации обещано снижение потребности в еде на 25%.
- В прототипе добавляется только `Stamina` с `critThreshold: 110` и комментарием
  `fix later`; модификатора голода там нет.
- Источники: `Resources/Prototypes/Imperial/Medieval/traits.yml`;
  `Resources/Locale/ru-RU/Imperial/Medieval/medieval.ftl`,
  ключ `trait-medieval-skills-food-desc`.
- При исправлении: согласовать нужный эффект, проверить его применение и
  взаимодействие с трейтом «Выносливый», также использующим `Stamina`.

## Водохлёб: описание не соответствует компонентам

- Трейт: `MedievalThirst`.
- В русской локализации обещано снижение потребности в воде на 25%.
- В прототипе добавляется только `Stamina` с `critThreshold: 110` и комментарием
  `fix later`; модификатора жажды там нет.
- Источники: `Resources/Prototypes/Imperial/Medieval/traits.yml`;
  `Resources/Locale/ru-RU/Imperial/Medieval/medieval.ftl`,
  ключ `trait-medieval-skills-thirst-desc`.
- При исправлении: согласовать нужный эффект, проверить его применение и
  взаимодействие с другими трейтами, добавляющими `Stamina`.

## Мастер меча: расходятся заявленный и заданный бонусы

- Трейт: `SkillOneHandedLargeSlash`.
- В описании прототипа указано +25% урона одноручными мечами.
- В `OneHandedLargeSlashSkillComponent` задано `DamageMult = 1.3f`, то есть +30%.
- Источники: `Resources/Prototypes/Imperial/Medieval/traits.yml`;
  `Content.Shared/Imperial/Medieval/WeaponSkillSystem/WeaponSkills.cs`;
  `Content.Shared/Imperial/Medieval/WeaponSkillSystem/WeaponSkillSystem.cs`.
- При исправлении: определить желаемое значение баланса и синхронизировать
  описание с реализацией. По одному расхождению нельзя считать ошибочным
  именно значение 30%.
