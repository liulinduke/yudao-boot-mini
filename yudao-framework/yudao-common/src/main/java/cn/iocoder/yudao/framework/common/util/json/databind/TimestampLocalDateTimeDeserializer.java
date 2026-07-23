package cn.iocoder.yudao.framework.common.util.json.databind;

import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.databind.DeserializationContext;
import com.fasterxml.jackson.databind.JsonDeserializer;

import java.io.IOException;
import java.time.Instant;
import java.time.LocalDateTime;
import java.time.OffsetDateTime;
import java.time.ZoneId;
import java.time.format.DateTimeFormatter;

/**
 * 基于时间戳的 LocalDateTime 反序列化器
 *
 * @author 老五
 */
public class TimestampLocalDateTimeDeserializer extends JsonDeserializer<LocalDateTime> {

    public static final TimestampLocalDateTimeDeserializer INSTANCE = new TimestampLocalDateTimeDeserializer();

    @Override
    public LocalDateTime deserialize(JsonParser p, DeserializationContext ctxt) throws IOException {
        if (p.currentToken() == com.fasterxml.jackson.core.JsonToken.VALUE_NULL) {
            return null;
        }

        // 兼容前端常用的毫秒时间戳。
        if (p.currentToken() == com.fasterxml.jackson.core.JsonToken.VALUE_NUMBER_INT) {
            return LocalDateTime.ofInstant(Instant.ofEpochMilli(p.getLongValue()), ZoneId.systemDefault());
        }

        String value = p.getValueAsString();
        if (value == null || value.isBlank()) {
            return null;
        }

        // CefSharp/WPF 和浏览器通常传 ISO-8601 UTC 时间，例如 2026-07-23T02:06:57.948Z。
        try {
            return OffsetDateTime.parse(value, DateTimeFormatter.ISO_OFFSET_DATE_TIME)
                    .toInstant()
                    .atZone(ZoneId.systemDefault())
                    .toLocalDateTime();
        } catch (Exception ignored) {
            // 继续兼容不带时区的 LocalDateTime 字符串。
        }
        try {
            return LocalDateTime.parse(value, DateTimeFormatter.ISO_LOCAL_DATE_TIME);
        } catch (Exception ex) {
            throw ctxt.weirdStringException(value, LocalDateTime.class,
                    "时间格式不支持，必须是毫秒时间戳或 ISO-8601 时间");
        }
    }

}
