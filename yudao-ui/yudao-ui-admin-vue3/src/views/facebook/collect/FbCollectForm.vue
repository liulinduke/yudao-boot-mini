<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" width="700px">
    <el-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-width="140px"
      v-loading="formLoading"
    >
      <el-form-item label="采集账号" prop="accountIds">
        <el-select
          v-model="formData.accountIds"
          multiple
          placeholder="请选择账号"
          style="width: 100%"
          filterable
          v-show="accounts.length > 0"
        >
          <el-option
            v-for="account in accounts"
            :key="account.id"
            :label="account.fbAccount + (account.remark ? '(' + account.remark + ')' : '')"
            :value="account.id"
          />
        </el-select>
        <!-- 当账号列表为空时显示提示信息 -->
        <div v-if="accounts.length === 0" class="el-select-empty">
          暂无可用账号，请先添加Facebook账号
        </div>
      </el-form-item>

      <el-form-item label="任务类型" prop="taskType">
        <el-select v-model="formData.taskType" placeholder="请选择任务类型" style="width: 100%">
          <el-option
            v-for="dict in getIntDictOptions(DICT_TYPE.FB_SEARCH_TYPE)"
            :key="dict.value"
            :label="dict.label"
            :value="dict.value"
          />
        </el-select>
      </el-form-item>

      <el-form-item label="采集链接" prop="searchUrl">
        <el-input
          v-model="formData.searchUrl"
          type="textarea"
          :rows="4"
          placeholder="请输入采集链接，多个链接请换行分隔。
在 Facebook 搜索目标内容后，复制浏览器地址栏的完整链接。
示例：https://facebook.com/search/people?q=关键词"
        />
      </el-form-item>

      <el-form-item label="期望数量" prop="expectedCount">
        <el-input-number
          v-model="formData.expectedCount"
          :min="1"
          :max="10000"
          style="width: 100%"
        />
      </el-form-item>

      <el-form-item label="采集间隔(秒)" prop="intervalSeconds">
        <el-input-number
          v-model="formData.intervalSeconds"
          :min="1"
          :max="300"
          style="width: 100%"
        />
      </el-form-item>

      <el-form-item label="备注" prop="remark">
        <el-input v-model="formData.remark" type="textarea" placeholder="请输入备注" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button type="primary" @click="submitForm" :loading="formLoading">确 定</el-button>
      <el-button @click="dialogVisible = false">取 消</el-button>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { Dialog } from '@/components/Dialog'
import { FbCollectApi, FbCollect } from '@/api/facebook/collect'
import { FbAccountApi } from '@/api/facebook/account'
import { startBrowserCollect } from '@/utils/wpfBridge'
import { DICT_TYPE, getIntDictOptions } from '@/utils/dict'
import { useI18n } from '@/hooks/web/useI18n'
import { useMessage } from '@/hooks/web/useMessage'

const { t } = useI18n() // 国际化
const message = useMessage() // 消息弹窗

const dialogVisible = ref(false) // 弹窗的是否展示
const dialogTitle = ref('') // 弹窗的标题
const formLoading = ref(false) // 表单的加载中：1）修改时的数据加载；2）提交的按钮禁用
const formType = ref('') // 表单的类型：create - 新增；update - 修改
const formData = ref({
  id: undefined,
  accountIds: [] as string[],
  taskType: undefined,
  searchUrl: '',
  expectedCount: 100,
  intervalSeconds: 5,
  remark: ''
})
const formRules = reactive({
  accountIds: [{ required: true, message: '请选择采集账号', trigger: 'change' }],
  taskType: [{ required: true, message: '请选择任务类型', trigger: 'change' }],
  searchUrl: [{ required: true, message: '请输入采集链接', trigger: 'blur' }],
  expectedCount: [{ required: true, message: '请输入期望数量', trigger: 'blur' }],
  intervalSeconds: [{ required: true, message: '请输入采集间隔', trigger: 'blur' }]
})
const formRef = ref() // 表单 Ref
const accounts = ref<any[]>([]) // 账号列表

/** 打开弹窗 */
const open = async (type: string, id?: number, accountList?: any[]) => {
  dialogVisible.value = true
  dialogTitle.value = t('action.' + type)
  formType.value = type
  resetForm()

  // 加载账号列表
  await loadAccounts()

  // 只有当 accountList 是一个有效数组时才使用它
  if (accountList && Array.isArray(accountList) && accountList.length > 0) {
    accounts.value = accountList
  }

  // 修改时，设置数据
  if (id) {
    formLoading.value = true
    try {
      const data = await FbCollectApi.getFbCollect(id)
      formData.value = data
    } finally {
      formLoading.value = false
    }
  }
}
defineExpose({ open }) // 提供 open 方法，用于打开弹窗

/** 加载账号列表 */
const loadAccounts = async () => {
  try {
    const data = await FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 200 })
    console.log('=== 加载账号列表 ===')
    console.log('API返回数据:', data)
    console.log('data.list:', data.list)

    if (data && data.list && Array.isArray(data.list)) {
      accounts.value = data.list
      console.log('赋值后的accounts.value:', accounts.value)
      console.log('第一个账号对象:', accounts.value[0])
      console.log('第一个账号的id:', accounts.value[0]?.id)
      console.log('第一个账号的fbAccount:', accounts.value[0]?.fbAccount)
      console.log('账号列表加载成功:', accounts.value.length, '个账号')
    } else {
      console.error('账号列表数据格式错误:', data)
      accounts.value = []
    }
  } catch (error) {
    console.error('加载账号列表失败:', error)
    accounts.value = []
  }
}

/** 提交表单 */
const emit = defineEmits(['success']) // 定义 success 事件，用于操作成功后的回调
const submitForm = async () => {
  // 校验表单
  if (!formRef.value) return
  await formRef.value.validate()
  // 提交请求
  formLoading.value = true
  try {
    const data = formData.value as unknown as FbCollect

    // 解析URL列表（支持换行分隔）
    const urls = (data.searchUrl || '').split('\n').filter((url) => url.trim())

    // 获取选中的账号信息
    const selectedAccounts = accounts.value.filter((acc) =>
      (data as any).accountIds?.includes(acc.id)
    )

    const createdTasks: Array<{ taskId: number; fbAccount: string; url: string }> = []

    // 为每个账号和URL组合创建任务并启动浏览器
    for (const account of selectedAccounts) {
      for (const url of urls) {
        const taskData: any = {
          fbAccount: account.fbAccount,
          taskType: data.taskType,
          searchUrl: url.trim(),
          expectedCount: data.expectedCount,
          intervalSeconds: data.intervalSeconds,
          status: 0, // 待执行
          remark: data.remark
        }

        // 创建任务
        let taskId: number
        if (formType.value === 'create') {
          const result = await FbCollectApi.createFbCollect(taskData)
          taskId = result.data || result // 根据后端返回结构调整
        } else {
          taskData.id = data.id
          await FbCollectApi.updateFbCollect(taskData)
          taskId = data.id!
        }

        // 立即启动浏览器采集
        try {
          startBrowserCollect(account.id, account.cookie || null, url.trim(), data.expectedCount)
          createdTasks.push({ taskId, fbAccount: account.fbAccount, url: url.trim() })
        } catch (error) {
          console.error(`启动账号 ${account.fbAccount} 的浏览器失败:`, error)
          message.warning(`账号 ${account.fbAccount} 启动浏览器失败`)
        }

        // 如果不是最后一个URL，等待间隔时间
        if (urls.indexOf(url) < urls.length - 1) {
          await new Promise((resolve) => setTimeout(resolve, data.intervalSeconds * 1000))
        }
      }
    }

    if (createdTasks.length > 0) {
      message.success(`已创建 ${createdTasks.length} 个任务并启动采集`)
      console.log('已启动的任务:', createdTasks)
    } else {
      message.success(
        t('common.' + (formType.value === 'create' ? 'createSuccess' : 'updateSuccess'))
      )
    }
    dialogVisible.value = false
    // 发送操作成功的事件
    emit('success')
  } finally {
    formLoading.value = false
  }
}

/** 重置表单 */
const resetForm = () => {
  formData.value = {
    id: undefined,
    accountIds: [],
    taskType: undefined,
    searchUrl: '',
    expectedCount: 100,
    intervalSeconds: 5,
    remark: ''
  }
  formRef.value?.resetFields()
}
</script>
