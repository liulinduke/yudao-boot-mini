package cn.iocoder.yudao.module.facebook.service.account;

import cn.hutool.core.collection.CollUtil;
import cn.hutool.core.util.StrUtil;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.springframework.stereotype.Service;
import jakarta.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.transaction.annotation.Transactional;

import java.time.LocalDateTime;
import java.util.*;
import java.util.concurrent.ThreadLocalRandom;
import java.util.stream.Collectors;
import cn.iocoder.yudao.module.facebook.controller.admin.account.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;

import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountActionStatDO;
import cn.iocoder.yudao.module.facebook.enums.OperationTypeEnum;
import cn.iocoder.yudao.module.facebook.service.dailylimit.FacebookDailyLimitService;
import cn.iocoder.yudao.module.system.dal.dataobject.SysProxyDO;
import cn.iocoder.yudao.module.system.service.SysProxyService;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.convertList;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.diffList;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.*;

/**
 * FB账号 Service 实现类
 *
 * @author 芋道源码
 */
@Service
@Validated
public class FbAccountServiceImpl implements FbAccountService {

    @Resource
    private FbAccountMapper fbAccountMapper;

    @Resource
    private FbAccountActionStatService actionStatService;

    @Resource
    private FacebookDailyLimitService dailyLimitService;

    @Resource
    private SysProxyService sysProxyService;

    @Override
    public Long createFbAccount(FbAccountSaveReqVO createReqVO) {
        FbAccountDO fbAccount = BeanUtils.toBean(createReqVO, FbAccountDO.class);
        ensureDeviceId(fbAccount);
        handleEmptyCookie(fbAccount);
        fbAccountMapper.insert(fbAccount);
        return fbAccount.getId();
    }

    @Override
    public void updateFbAccount(FbAccountSaveReqVO updateReqVO) {
        validateFbAccountExists(updateReqVO.getId());
        FbAccountDO updateObj = BeanUtils.toBean(updateReqVO, FbAccountDO.class);
        handleEmptyCookie(updateObj);
        fbAccountMapper.updateById(updateObj);
    }

    @Override
    public void updateFbAccountStatus(List<Long> ids, Boolean status) {
        if (CollUtil.isEmpty(ids)) {
            return;
        }
        for (Long id : ids) {
            if (id == null) {
                continue;
            }
            FbAccountDO updateObj = new FbAccountDO();
            updateObj.setId(id);
            updateObj.setStatus(status);
            fbAccountMapper.updateById(updateObj);
        }
    }

    @Override
    public void deleteFbAccount(Long id) {
        validateFbAccountExists(id);
        fbAccountMapper.deleteById(id);
    }

    @Override
    public void deleteFbAccountListByIds(List<Long> ids) {
        fbAccountMapper.deleteByIds(ids);
    }

    private void validateFbAccountExists(Long id) {
        if (fbAccountMapper.selectById(id) == null) {
            throw exception(FB_ACCOUNT_NOT_EXISTS);
        }
    }

    @Override
    public FbAccountDO getFbAccount(Long id) {
        return fbAccountMapper.selectById(id);
    }

    @Override
    public FbAccountRuntimeProxyRespVO getRuntimeProxy(String accountId) {
        if (StrUtil.isBlank(accountId)) {
            throw new IllegalArgumentException("账号不能为空");
        }
        FbAccountDO account = null;
        try {
            account = fbAccountMapper.selectById(Long.valueOf(accountId));
        } catch (NumberFormatException ignored) {
            // 任务有时携带 Facebook 账号字符串，继续按 fb_account 查询。
        }
        if (account == null) {
            account = fbAccountMapper.selectOne(new LambdaQueryWrapperX<FbAccountDO>()
                    .eq(FbAccountDO::getFbAccount, accountId));
        }
        if (account == null) {
            throw new IllegalStateException("FB账号不存在: " + accountId);
        }
        if (account.getProxyId() == null) {
            return null;
        }

        SysProxyDO proxy = sysProxyService.getProxyDO(account.getProxyId());
        if (proxy == null) {
            throw new IllegalStateException("账号绑定的代理不存在: " + account.getProxyId());
        }
        if (!Integer.valueOf(1).equals(proxy.getStatus())) {
            throw new IllegalStateException("账号绑定的代理已禁用: " + account.getProxyId());
        }
        if (proxy.getProxyType() == null || proxy.getProxyType() < 1 || proxy.getProxyType() > 3
                || StrUtil.isBlank(proxy.getHost()) || proxy.getPort() == null
                || proxy.getPort() < 1 || proxy.getPort() > 65535) {
            throw new IllegalStateException("账号绑定的代理配置不完整: " + account.getProxyId());
        }

        FbAccountRuntimeProxyRespVO result = new FbAccountRuntimeProxyRespVO();
        result.setProxyType(proxy.getProxyType());
        result.setHost(proxy.getHost().trim());
        result.setPort(proxy.getPort());
        result.setUsername(StrUtil.isBlank(proxy.getUsername()) ? null : proxy.getUsername());
        result.setPassword(StrUtil.isBlank(proxy.getPassword()) ? null : proxy.getPassword());
        return result;
    }

    @Override
    public PageResult<FbAccountDO> getFbAccountPage(FbAccountPageReqVO pageReqVO) {
        return fbAccountMapper.selectPage(pageReqVO);
    }

    @Override
    public List<FbAccountSelectorOptionRespVO> getSelectorOptions(FbAccountSelectorOptionReqVO reqVO) {
        FbAccountPageReqVO pageReqVO = new FbAccountPageReqVO();
        pageReqVO.setPageSize(PageParam.PAGE_SIZE_NONE);
        pageReqVO.setStatus(true);
        // 账号候选接口只返回启用账号。分页条件用于数据库过滤，服务层再兜底一次，
        // 避免不同数据库或历史查询逻辑导致停用账号进入任务选择器。
        List<FbAccountDO> accounts = fbAccountMapper.selectPage(pageReqVO).getList().stream()
                .filter(account -> Boolean.TRUE.equals(account.getStatus()))
                .collect(Collectors.toList());
        List<Long> accountIds = accounts.stream().map(FbAccountDO::getId).toList();
        Map<Long, List<FbAccountActionStatDO>> statMap = actionStatService.getByAccountIds(accountIds).stream()
                .collect(Collectors.groupingBy(FbAccountActionStatDO::getAccountId));

        List<String> actionTypes = reqVO == null || CollUtil.isEmpty(reqVO.getActionTypes())
                ? Collections.emptyList() : reqVO.getActionTypes().stream()
                .flatMap(value -> Arrays.stream(value.split(",")))
                .map(String::trim).filter(StrUtil::isNotBlank).distinct().collect(Collectors.toList());
        List<FbAccountSelectorOptionRespVO> result = new ArrayList<>();
        for (FbAccountDO account : accounts) {
            FbAccountSelectorOptionRespVO option = new FbAccountSelectorOptionRespVO();
            option.setId(account.getId());
            option.setFbAccount(account.getFbAccount());
            option.setGroupId(account.getGroupId());
            option.setStatus(account.getStatus());
            option.setLoginStatus(account.getLoginStatus());
            option.setEligible(isSelectableStatus(account.getLoginStatus()));
            option.setDisabledReason(option.getEligible() ? "" : loginStatusReason(account.getLoginStatus()));

            for (OperationTypeEnum type : OperationTypeEnum.values()) {
                int limit = dailyLimitService.getConfiguredLimit(type);
                int remaining = dailyLimitService.getRemainingCount(String.valueOf(account.getId()), type);
                option.getLimits().put(type.getCode(), limit);
                option.getToday().put(type.getCode(), Math.max(0, limit - remaining));
                if (actionTypes.contains(type.getCode()) && remaining <= 0) {
                    option.setEligible(false);
                    option.setDisabledReason("今日" + type.getName() + "已达上限");
                }
            }

            Map<String, Long> totals = option.getTotal();
            totals.put("taskCount", 0L);
            totals.put("dm", 0L);
            totals.put("repost", 0L);
            totals.put("join_group", 0L);
            totals.put("group_post", 0L);
            totals.put("comment", 0L);
            totals.put("follow", 0L);
            totals.put("collect", 0L);
            for (FbAccountActionStatDO stat : statMap.getOrDefault(account.getId(), Collections.emptyList())) {
                totals.put("taskCount", totals.get("taskCount") + value(stat.getTotalTaskCount()));
                totals.put(stat.getActionType(), value(stat.getTotalActionCount()));
                if ("collect".equals(stat.getActionType())) {
                    totals.put("collect", value(stat.getTotalCollectCount()));
                }
                if (option.getLastExecuteTime() == null ||
                        (stat.getLastExecuteTime() != null && stat.getLastExecuteTime().isAfter(option.getLastExecuteTime()))) {
                    option.setLastExecuteTime(stat.getLastExecuteTime());
                }
            }
            result.add(option);
        }
        return result;
    }

    private boolean isSelectableStatus(String status) {
        return StrUtil.isBlank(status) || "SUCCESS".equalsIgnoreCase(status) || "PENDING".equalsIgnoreCase(status);
    }

    private String loginStatusReason(String status) {
        if ("COOKIE_INVALID".equalsIgnoreCase(status) || "COOKIE_EXPIRED".equalsIgnoreCase(status)) return "Cookie失效";
        if ("ABNORMAL".equalsIgnoreCase(status) || "INVALID".equalsIgnoreCase(status)) return "账号异常";
        if ("FAILED".equalsIgnoreCase(status)) return "账号需要重新检测";
        return "账号状态不可用";
    }

    private long value(Long value) {
        return value == null ? 0L : value;
    }


    @Override
    public void updateFbAccountLanguage(Long id, String languageCode) {
        validateFbAccountExists(id);
        if (StrUtil.isBlank(languageCode)) {
            throw new IllegalArgumentException("语言代码不能为空");
        }
        FbAccountDO updateObj = new FbAccountDO();
        updateObj.setId(id);
        updateObj.setLanguageCode(languageCode);
        if ("en_US".equalsIgnoreCase(languageCode)) {
            updateObj.setLanguage(1);
        } else if (languageCode.toLowerCase(Locale.ROOT).startsWith("zh_")) {
            updateObj.setLanguage(2);
        }
        fbAccountMapper.updateById(updateObj);
    }


    @Override
    @Transactional(rollbackFor = Exception.class)
    public void updateFbAccountProxy(List<Long> ids, Long proxyId) {
        for (Long id : ids) {
            FbAccountDO updateObj = new FbAccountDO();
            updateObj.setId(id);
            updateObj.setProxyId(proxyId);
            fbAccountMapper.updateById(updateObj);
        }
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void saveProfileUpload(FbAccountProfileUploadReqVO reqVO) {
        for (FbAccountProfileUploadReqVO.Item item : reqVO.getItems()) {
            FbAccountDO updateObj = new FbAccountDO();
            updateObj.setId(item.getAccountId());
            updateObj.setProfileUpdateStatus("PENDING");
            updateObj.setProfileUpdateError(null);
            fbAccountMapper.updateById(updateObj);
        }
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void reportProfileUpload(FbAccountProfileReportReqVO reqVO) {
        FbAccountDO updateObj = new FbAccountDO();
        updateObj.setId(reqVO.getAccountId());
        updateObj.setProfileUpdateStatus(reqVO.getStatus());
        updateObj.setProfileUpdateTime(java.time.LocalDateTime.now());
        updateObj.setProfileUpdateError(reqVO.getErrorMessage());
        // 待上传资料只存在于 WPF 任务配置中，只有 Facebook 实际执行成功后才覆盖当前资料。
        if ("SUCCESS".equalsIgnoreCase(reqVO.getStatus())) {
            if (reqVO.getAvatarUrl() != null) updateObj.setAvatarUrl(reqVO.getAvatarUrl());
            if (reqVO.getCoverUrl() != null) updateObj.setCoverUrl(reqVO.getCoverUrl());
            if (reqVO.getNickname() != null) updateObj.setProfileNickname(reqVO.getNickname());
            if (reqVO.getSignature() != null) updateObj.setProfileSignature(reqVO.getSignature());
        }
        fbAccountMapper.updateById(updateObj);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void updateFbAccountGroup(List<Long> ids, Long groupId) {
        for (Long id : ids) {
            FbAccountDO updateObj = new FbAccountDO();
            updateObj.setId(id);
            updateObj.setGroupId(groupId);
            fbAccountMapper.updateById(updateObj);
        }
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void importFbAccount(FbAccountImportReqVO importReqVO) {
        LocalDateTime now = LocalDateTime.now();

        for (String[] parts : parseAccountImportRecords(importReqVO.getData())) {
            if (parts.length < 2) {
                continue;
            }

            String userName = parts[0].trim();
            String password = parts[1].trim();
            String securityKey = parts.length > 2 ? parts[2].trim() : null;
            String cookie = parts.length > 3 ? parts[3].trim() : null;

            if (StrUtil.isEmpty(userName) || StrUtil.isEmpty(password)) {
                continue;
            }

            FbAccountDO account = new FbAccountDO();
            account.setFbAccount(userName);
            account.setPassword(password);
            account.setTfa(securityKey);
            account.setGroupId(importReqVO.getGroupId());
            account.setProxyId(importReqVO.getProxyId());
            account.setStatus(true);
            account.setCreateTime(now);
            account.setUpdateTime(now);
            account.setCookie(normalizeCookieForStorage(cookie));
            ensureDeviceId(account);
            handleEmptyCookie(account);

            fbAccountMapper.insert(account);
        }
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void importFbAccountCookie(FbAccountCookieImportReqVO importReqVO) {
        String data = importReqVO.getData();
        List<String> cookieEntries = splitCookieEntries(data);
        LocalDateTime now = LocalDateTime.now();

        for (String line : cookieEntries) {
            line = line.trim();
            if (StrUtil.isEmpty(line)) {
                continue;
            }

            String userId = extractUserIdFromCookie(line);
            if (StrUtil.isEmpty(userId)) {
                continue;
            }

            FbAccountDO account = new FbAccountDO();
            account.setFbAccount(userId);
            account.setCookie(normalizeCookieForStorage(line));
            account.setGroupId(importReqVO.getGroupId());
            account.setProxyId(importReqVO.getProxyId());
            account.setStatus(true);
            account.setCreateTime(now);
            account.setUpdateTime(now);
            ensureDeviceId(account);

            fbAccountMapper.insert(account);
        }
    }

    @Override
    public void updateFbAccountLoginResult(FbAccountLoginResultUpdateReqVO reqVO) {
        // 网络异常只代表本次检测失败，不能覆盖账号原有登录状态。
        if ("NETWORK_ERROR".equalsIgnoreCase(reqVO.getLoginStatus())) {
            return;
        }
        validateFbAccountExists(reqVO.getId());

        FbAccountDO updateObj = new FbAccountDO();
        updateObj.setId(reqVO.getId());
        updateObj.setLoginStatus(reqVO.getLoginStatus());
        updateObj.setLoginErrorReason(reqVO.getLoginErrorReason());
        if (StrUtil.isNotBlank(reqVO.getCookie())) {
            updateObj.setCookie(reqVO.getCookie());
        }
        if ("SUCCESS".equalsIgnoreCase(reqVO.getLoginStatus())) {
            updateObj.setLoginErrorReason(null);
            updateObj.setLastLoginTime(LocalDateTime.now());
        }
        fbAccountMapper.updateById(updateObj);
    }

    private String extractUserIdFromCookie(String cookie) {
        if (StrUtil.isEmpty(cookie)) {
            return null;
        }

        try {
            int start = cookie.indexOf("c_user=");
            if (start != -1) {
                int end = cookie.indexOf(";", start);
                if (end == -1) {
                    end = cookie.length();
                }
                return cookie.substring(start + 7, end);
            }

            JsonNode json = new ObjectMapper().readTree(cookie);
            if (json.isArray()) {
                for (JsonNode item : json) {
                    if ("c_user".equals(item.path("name").asText())) {
                        String value = item.path("value").asText();
                        if (StrUtil.isNotBlank(value)) return value;
                    }
                }
            } else if (json.isObject() && json.has("c_user")) {
                String value = json.path("c_user").asText();
                if (StrUtil.isNotBlank(value)) return value;
            }
        } catch (Exception e) {
            // ignore
        }

        return null;
    }

    private List<String> splitCookieEntries(String data) {
        String trimmedData = StrUtil.trim(data);
        if (StrUtil.isEmpty(trimmedData)) {
            return Collections.emptyList();
        }

        try {
            JsonNode json = new ObjectMapper().readTree(trimmedData);
            if (json != null && (json.isArray() || json.isObject())) {
                return Collections.singletonList(trimmedData);
            }
        } catch (Exception ignored) {
            // 不是完整 JSON 时，继续按兼容的每行 Cookie 字符串处理。
        }

        return Arrays.stream(data.split("\\r?\\n"))
                .map(String::trim)
                .filter(StrUtil::isNotEmpty)
                .collect(Collectors.toList());
    }

    private void handleEmptyCookie(FbAccountDO account) {
        account.setCookie(normalizeCookieForStorage(account.getCookie()));
    }

    /** 数据库 cookie 字段是 JSON；兼容供应商常见的 name=value;name2=value2 文本格式。 */
    private String normalizeCookieForStorage(String cookie) {
        if (StrUtil.isBlank(cookie)) {
            return "[]";
        }
        String value = cookie.trim();
        ObjectMapper mapper = new ObjectMapper();
        try {
            JsonNode json = mapper.readTree(value);
            if (json != null && json.isArray()) {
                return json.toString();
            }
            if (json != null && json.isObject()) {
                // 单个 Cookie 对象也转成 CefSharp 使用的数组格式。
                if (json.has("name") && json.has("value")) {
                    return mapper.createArrayNode().add(json).toString();
                }
                var array = mapper.createArrayNode();
                json.fields().forEachRemaining(entry -> array.addObject()
                        .put("name", entry.getKey())
                        .put("value", entry.getValue().asText())
                        .put("domain", ".facebook.com")
                        .put("path", "/")
                        .put("secure", true));
                return array.toString();
            }
        } catch (Exception ignored) {
            // 原始 Cookie 文本，继续转换。
        }

        var array = mapper.createArrayNode();
        for (String part : value.split(";")) {
            int separator = part.indexOf('=');
            if (separator <= 0) continue;
            String name = part.substring(0, separator).trim();
            String cookieValue = part.substring(separator + 1).trim();
            if (StrUtil.isBlank(name)) continue;
            array.addObject()
                    .put("name", name)
                    .put("value", cookieValue)
                    .put("domain", ".facebook.com")
                    .put("path", "/")
                    .put("secure", true)
                    .put("httpOnly", false);
        }
        return array.toString();
    }

    /**
     * 解析账号导入数据。新格式为：账号|密码|2FA|Cookie|Token|邮箱；Token 和邮箱不入库。
     * 同时兼容旧格式：账号----密码----2FA，以及多条记录连续粘贴在同一行的情况。
     */
    private List<String[]> parseAccountImportRecords(String data) {
        List<String[]> records = new ArrayList<>();
        if (StrUtil.isBlank(data)) {
            return records;
        }

        // 下一条记录以数字账号开头，供应商数据常表现为“| 615...|”。
        String normalized = data.replaceAll("\\|\\s*(?=\\d{10,}\\s*\\|)", "\\n");
        for (String line : normalized.split("\\R")) {
            String trimmedLine = line.trim();
            if (StrUtil.isEmpty(trimmedLine)) {
                continue;
            }

            if (trimmedLine.contains("----")) {
                records.add(trimmedLine.split("----", -1));
            } else if (trimmedLine.contains("|")) {
                records.add(trimmedLine.split("\\|", -1));
            }
        }
        return records;
    }

    /**
     * 设备 ID 是账号级固定指纹标识。新增和导入时未填写则自动生成，
     * 后续浏览器会基于该值生成稳定的设备名称。
     */
    private void ensureDeviceId(FbAccountDO account) {
        if (account.getDeviceId() == null || account.getDeviceId() == 0L) {
            account.setDeviceId(ThreadLocalRandom.current().nextLong(
                    1_000_000_000_000L, 9_000_000_000_000_000L));
        }
    }

}
