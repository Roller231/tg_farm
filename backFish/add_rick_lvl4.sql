-- Добавление нового продукта Rick (уровень 4) для дома 1
-- Этот Rick покупается сразу 4 уровня за 250 TON и добывает 1 TON/3 часа

INSERT INTO products (name, type, price, sell_price, speed_price, lvl_for_buy, time, exp, image_seed_link, image_ready_link)
VALUES (
    'Rick',                                                          -- name
    'home1',                                                         -- type
    250.00,                                                          -- price (250 TON)
    1.00,                                                            -- sell_price (1 TON за цикл)
    5.00,                                                            -- speed_price (для ускорения)
    1,                                                               -- lvl_for_buy (доступен с 1 уровня)
    10800,                                                           -- time (3 часа = 10800 секунд)
    0.04,                                                            -- exp
    'https://farmbeachtig.st8.ru/images/rick.jpg',                  -- image_seed_link
    'https://farmbeachtig.st8.ru/images/rick.jpg'                   -- image_ready_link
);

-- Примечание: Этот Rick отличается от существующего Rick уровня 1:
-- - Старый Rick: покупается за монеты, прокачивается до 4 уровня, добывает "До 1 TON/3ч"
-- - Новый Rick: покупается сразу 4 уровня за 250 TON, добывает "1 TON/3ч" (фиксированно)
