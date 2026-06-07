# trash-compactor
s&amp;box проект.

## Рабочие системы

- **Gameplay (`code/Gameplay.cs`)**: компонент-синглтон, инициализирующий игровой цикл и вызывающий спавн локального игрока через `Rpc.Broadcast` (HostOnly). Часть логики ролей закомментирована.
- **Player (`code/Player/Player.cs`)**: синглтон `Player.Local`, хранит `Role`, синхронизирует `RoleEnum` и `Name`, реализует `IDamageable`, выполняет спавн в случайной точке роли с `Jump`-воркэраундом против застревания.
- **FpPlayerGrabber (`code/Player/FpPlayerGrabber.cs`)**: физический грэббер от первого лица — захват и перенос тел через `PhysicsBody.SmoothMove` по `attack1`, импульсный пуш с уроном по рейкасту на `attack2`, эффекты попадания и декали.
- **Roles (`code/Role/*`)**: абстрактный `Role` со списком `Spawns` и проверками по типу/строке; реализации `Trashman`, `Survival`, `Spectator` плюс enum `RoleTrashCompactor` для сетевой синхронизации.
- **RoundManager (`code/RoundManager/RoundManager.cs`)**: серверный конечный автомат раундов (`RoundState`) с `TimeUntil` таймером, синхронизацией времени по RPC (`RequestSyncToHostRpc`/`SendSyncToClientsRpc`) и автоциклом Start↔Finish; респавн игрока по окончании раунда.
- **Trash (`code/Trash/Trash.cs`, `SpawnerTrash.cs`)**: мусор как `ICollisionListener`, наносящий игроку урон пропорционально скорости `Rigidbody` при столкновении; есть спавнер мусора.
- **MapInfo (`code/Map/MapInfo.cs`)**: синглтон-компонент карты со списками точек спавна для `Trashman`/`Survival`/`Spectator`.
- **UI (`code/UI/Hud.razor`, `ScoreMenu.razor`)**: Razor-HUD с именем игрока, состоянием раунда, таймером, HP/Armor; отдельное меню счёта.
