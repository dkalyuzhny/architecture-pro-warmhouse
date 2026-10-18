-- Create the database if it doesn't exist
CREATE DATABASE smarthome;

-- Connect to the database
\c smarthome;

-- Create the sensors table
CREATE TABLE IF NOT EXISTS sensors (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    type VARCHAR(50) NOT NULL,
    location VARCHAR(100) NOT NULL,
    value FLOAT DEFAULT 0,
    unit VARCHAR(20),
    status VARCHAR(20) NOT NULL DEFAULT 'inactive',
    last_updated TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    email VARCHAR(255) NOT NULL UNIQUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS houses (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id),
    name VARCHAR(100) NOT NULL,
    address VARCHAR(255),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS device_types (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    description VARCHAR(255),
    unit VARCHAR(20)
);

CREATE TABLE IF NOT EXISTS devices (
    id SERIAL PRIMARY KEY,

    house_id INTEGER NOT NULL REFERENCES houses(id),
    type_id INTEGER NOT NULL REFERENCES device_types(id),

    serial_number VARCHAR(100) NOT NULL UNIQUE,

    name VARCHAR(100) NOT NULL,
    location VARCHAR(100) NOT NULL,

    status VARCHAR(20) NOT NULL DEFAULT 'inactive',

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS telemetry_data (
    id BIGSERIAL PRIMARY KEY,

    device_id INTEGER NOT NULL REFERENCES devices(id),

    value DOUBLE PRECISION NOT NULL,
    status VARCHAR(20) NOT NULL,

    timestamp TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);


CREATE INDEX idx_devices_house_id
    ON devices(house_id);

CREATE INDEX idx_devices_type_id
    ON devices(type_id);

CREATE INDEX idx_telemetry_device_id
    ON telemetry_data(device_id);

CREATE INDEX idx_telemetry_device_timestamp
    ON telemetry_data(device_id, timestamp DESC);



INSERT INTO users(name, email)
VALUES ('Demo User', 'demo_user@yandex.ru')
ON CONFLICT(email) DO NOTHING;


INSERT INTO houses(user_id, name, address)
SELECT id, 'Demo House', 'Demo address'
FROM users
WHERE email = 'demo_user@yandex.ru'
AND NOT EXISTS (
    SELECT 1
    FROM houses
    WHERE name = 'Demo House'
);


INSERT INTO device_types(name, description, unit)
VALUES (
    'temperature',
    'Temperature sensor',
    'C'
)
ON CONFLICT(name) DO NOTHING;

-- Create indexes for common queries
CREATE INDEX IF NOT EXISTS idx_sensors_type ON sensors(type);
CREATE INDEX IF NOT EXISTS idx_sensors_location ON sensors(location);
CREATE INDEX IF NOT EXISTS idx_sensors_status ON sensors(status);