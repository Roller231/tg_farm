import json
import aiohttp
import asyncio
from aiogram import Bot, Dispatcher, types
from aiogram.filters import CommandStart, CommandObject
from aiogram.types import InlineKeyboardMarkup, InlineKeyboardButton

API_TOKEN = "8432053231:AAG7Bq4NUgguRefZLh2pBLoJL0pGKtg-HFs"
BACKEND_URL = "https://farmbeachtg.st8.ru/api/usedcc/users"

bot = Bot(token=API_TOKEN)
dp = Dispatcher()

# ---------- Кнопка "Начать игру" ----------
start_button = InlineKeyboardMarkup(
    inline_keyboard=[
        [
            InlineKeyboardButton(
                text="🎮 Начать игру",
                url="https://t.me/FIshFarmers_bot/farmFish"  # сюда вставляешь ссылку на старт
            )
        ]
    ]
)



# ---------- GET user ----------
async def get_user(user_id: str):
    async with aiohttp.ClientSession() as session:
        async with session.get(f"{BACKEND_URL}/{user_id}") as resp:
            if resp.status == 200:
                return await resp.json()
            return None


# ---------- PUT user ----------
async def update_user(user_id: str, data: dict):
    async with aiohttp.ClientSession() as session:
        async with session.put(f"{BACKEND_URL}/{user_id}", json=data) as resp:
            return resp.status, await resp.text()


# ---------- POST create user ----------
async def create_user(user: types.User, ref_id: str | None):
    payload = {
        "id": str(user.id),
        "name": user.username or "Unknown",
        "firstName": user.first_name,
        "ton": 0,
        "lvl_upgrade": 0,
        "lvl": 1,
        "coin": "100",
        "bezoz": "10",
        "ref_count": 0,
        "refId": ref_id or "",
        "isPremium": 0,
        "time_farm": "",
        "seed_count": json.dumps({"items": []}, ensure_ascii=False),
        "storage_count": json.dumps({"items": []}, ensure_ascii=False),
        "grid_count": 3,
        "grid_state": "",
        "houses": json.dumps({
            "items": [
                {"id": 1, "price": 100, "lvl_for_buy": 1, "build_time": 3600, "active": False, "type": "home1", "timers": []},
                {"id": 2, "price": 500, "lvl_for_buy": 2, "build_time": 7200, "active": False, "type": "home2", "timers": []},
                {"id": 3, "price": 1000, "lvl_for_buy": 3, "build_time": 14400, "active": False, "type": "home3", "timers": []},
                {"id": 4, "price": 2000, "lvl_for_buy": 4, "build_time": 28800, "active": True, "type": "mine", "timers": []},
                {"id": 5, "price": 2500, "lvl_for_buy": 5, "build_time": 36000, "active": True, "type": "voyage", "timers": []}
            ]
        }, ensure_ascii=False)
    }

    async with aiohttp.ClientSession() as session:
        async with session.post(BACKEND_URL, json=payload) as resp:
            if resp.status == 201:
                print("✅ User created OK")
            elif resp.status == 409:
                print("⚠️ User already exists")
            else:
                print("❌ Error creating user:", resp.status, await resp.text())


# ---------- REFERRAL reward ----------
async def reward_referrer(ref_id: str):
    ref_user = await get_user(ref_id)
    if not ref_user:
        print(f"❌ Referrer {ref_id} not found")
        return

    try:
        print("📥 Текущий ref_user:", ref_user)

        current_coin = float(ref_user.get("coin", "0"))
        current_refs = int(ref_user.get("ref_count", 0))

        # обновляем монеты и счётчик рефералов
        ref_user["coin"] = str(int(current_coin) + 100)
        ref_user["ref_count"] = current_refs + 1

        print("📤 Отправляем в backend:", ref_user)

        status, text = await update_user(ref_id, ref_user)

        if status == 200:
            print(f"✅ Referrer {ref_id} получил +100 монет")
        else:
            print(f"⚠️ Ошибка обновления ({status}): {text}")

    except Exception as e:
        print("❌ Ошибка при обновлении реферала:", e)


# ---------- START with referral ----------
@dp.message(CommandStart(deep_link=True))
async def start_with_ref(message: types.Message, command: CommandObject):
    ref_id = command.args  # id пригласившего игрока

    # запрет на самореферал
    if ref_id == str(message.from_user.id):
        await message.answer(
            "❌ Ой! Нельзя использовать собственную реферальную ссылку! ❌\n\n"
            "👉 Нажми кнопку ниже, чтобы начать игру.",
            reply_markup=start_button
        )
        ref_id = None

    # проверяем, есть ли игрок в базе
    existing_user = await get_user(str(message.from_user.id))
    if existing_user:
        await message.answer(
            f"⚠️ Привет, {message.from_user.first_name}! ⚠️\n\n"
            "💡 Похоже, ты уже играешь в нашу игру!\n"
            "Бонус реферу не начисляется.\n\n"
            "🔥 Нажми кнопку ниже, чтобы продолжить приключение!",
            reply_markup=start_button
        )
        return

    # создаём нового игрока
    await create_user(message.from_user, ref_id)

    # если есть реферал — начисляем бонус
    if ref_id:
        await reward_referrer(ref_id)
        ref_user = await get_user(ref_id)
        ref_name = ref_user.get("firstName", "Unknown") if ref_user else "Unknown"

        await message.answer(
            f"🎉 Привет, {message.from_user.first_name}! 🎉\n\n"
            f"✨ Ты присоединился по реферальной ссылке {ref_name} и получил стартовые бонусы!\n"
            "💰 100 монет\n"
            "🌱 10 безоз\n\n"
            "🚀 Пора начать своё удивительное приключение!\n"
            "Нажми кнопку ниже и вперед к игре!",
            reply_markup=start_button
        )
    else:
        await message.answer(
            f"🎉 Привет, {message.from_user.first_name}! 🎉\n\n"
            "✨ Ты начал игру без реферала и получил стартовые бонусы!\n"
            "💰 100 монет\n"
            "🌱 10 безоз\n\n"
            "🚀 Пора отправляться в приключение!\n"
            "Нажми кнопку ниже и вперед к игре!",
            reply_markup=start_button
        )


# ---------- START without referral ----------
@dp.message(CommandStart())
async def start_no_ref(message: types.Message):
    existing_user = await get_user(str(message.from_user.id))
    if existing_user:
        await message.answer(
            f"⚠️ Привет, {message.from_user.first_name}! ⚠️\n\n"
            "💡 Ты уже играешь в нашу игру.\n\n"
            "🔥 Нажми кнопку ниже, чтобы продолжить приключение!",
            reply_markup=start_button
        )
        return

    await create_user(message.from_user, None)
    await message.answer(
        f"🎉 Привет, {message.from_user.first_name}! 🎉\n\n"
        "✨ Ты начал игру без реферала и получил стартовые бонусы!\n"
        "💰 100 монет\n"
        "🌱 10 безоз\n\n"
        "🚀 Пора отправляться в приключение!\n"
        "Нажми кнопку ниже и вперед к игре!",
        reply_markup=start_button
    )


# ---------- RUN ----------
async def main():
    print("🤖 Bot started")
    await dp.start_polling(bot)


if __name__ == "__main__":
    asyncio.run(main())
