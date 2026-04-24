<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" width="900px">
    <el-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-width="120px"
      v-loading="formLoading"
    >
      <el-form-item label="任务名称" prop="taskName">
        <el-input v-model="formData.taskName" placeholder="请输入任务名称" />
      </el-form-item>

      <!-- 1. 选择目标潜客 -->
      <el-form-item label="选择目标潜客" prop="targetUserIds">
        <div class="w-full">
          <el-button type="primary" @click="openUserSelector" class="mb-2">
            <Icon icon="ep:plus" class="mr-5px" /> 选择目标潜客
          </el-button>
          <div v-if="selectedUsers.length > 0" class="mt-2">
            <el-tag
              v-for="user in selectedUsers.slice(0, 10)"
              :key="user.fbUserId"
              closable
              @close="removeSelectedUser(user.fbUserId)"
              class="mr-2 mb-2"
              size="small"
            >
              {{ user.userName }}
            </el-tag>
            <span v-if="selectedUsers.length > 10" class="text-sm text-gray-500">
              等{{ selectedUsers.length }}个用户
            </span>
            <div class="text-gray-500 text-sm mt-2">
              已选择 {{ selectedUsers.length }} 个目标用户
            </div>
          </div>
          <div v-else class="text-gray-400 text-sm mt-2">
            暂未选择用户，请点击上方按钮选择
          </div>
        </div>
      </el-form-item>

      <!-- 2. 选择群发话术 -->
      <el-form-item label="群发话术" prop="scripts">
        <div class="w-full">
          <el-radio-group v-model="scriptType" class="mb-2">
            <el-radio :label="1">手动输入</el-radio>
            <el-radio :label="2">从话术库选择</el-radio>
          </el-radio-group>

          <!-- 手动输入模式 -->
          <el-input
            v-if="scriptType === 1"
            v-model="manualScripts"
            type="textarea"
            :rows="4"
            placeholder="请输入话术，每条话术占一行"
          />

          <!-- 话术库模式 -->
          <div v-else>
            <el-button type="primary" @click="openScriptSelector" class="mb-2">
              <Icon icon="ep:plus" class="mr-5px" /> 从话术库选择
            </el-button>
            <div v-if="selectedScripts.length > 0" class="mt-2">
              <el-tag
                v-for="(script, index) in selectedScripts.slice(0, 5)"
                :key="index"
                closable
                @close="removeSelectedScript(index)"
                class="mr-2 mb-2"
                size="small"
              >
                {{ script.substring(0, 30) }}...
              </el-tag>
              <span v-if="selectedScripts.length > 5" class="text-sm text-gray-500">
                等{{ selectedScripts.length }}条话术
              </span>
            </div>
          </div>

          <!-- 随机表情 -->
          <el-checkbox v-model="formData.appendRandomEmoji" class="mt-2">
            追加随机表情
          </el-checkbox>
        </div>
      </el-form-item>

      <!-- 3. 执行账号 -->
      <el-form-item label="执行账号" prop="accountIds">
        <div class="w-full">
          <el-select
            v-model="formData.accountIds"
            multiple
            filterable
            placeholder="请选择执行账号"
            class="w-full"
          >
            <el-option
              v-for="group in accountGroups"
              :key="group.id"
              :label="group.groupName"
              :value="group.id"
              disabled
            >
              <span class="font-bold">{{ group.groupName }}</span>
            </el-option>
            <el-option
              v-for="account in filteredAccounts"
              :key="account.id"
              :label="`${account.fbAccount} (剩余: ${account.remainingDm || 0})`"
              :value="String(account.id)"
            >
              <span>{{ account.fbAccount }}</span>
              <span class="text-gray-400 ml-2">剩余: {{ account.remainingDm || 0 }}</span>
            </el-option>
          </el-select>
          <div class="text-gray-500 text-sm mt-2">
            已选择 {{ formData.accountIds.length }} 个账号
          </div>
        </div>
      </el-form-item>

      <!-- 4. 群发间隔 -->
      <el-form-item label="群发间隔" prop="intervalRange">
        <el-select v-model="intervalRange" placeholder="请选择间隔范围" class="!w-200px">
          <el-option label="2-4秒" value="2-4" />
          <el-option label="4-10秒" value="4-10" />
          <el-option label="10-16秒" value="10-16" />
        </el-select>
        <span class="ml-10px text-gray-500">每个账号发送消息的随机间隔时间</span>
      </el-form-item>

      <!-- 5. 备注 -->
      <el-form-item label="备注" prop="remark">
        <el-input
          v-model="formData.remark"
          type="textarea"
          placeholder="请输入备注"
          :rows="2"
        />
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="submitForm" type="primary" :disabled="formLoading">确 定</el-button>
      <el-button @click="dialogVisible = false">取 消</el-button>
    </template>
  </Dialog>

  <!-- 用户选择器 -->
  <UserSelector v-model="userSelectorVisible" @confirm="handleUserConfirm" />

  <!-- 话术选择器 -->
  <ScriptSelector v-model="scriptSelectorVisible" @confirm="handleScriptConfirm" />
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { DmTaskApi } from '@/api/facebook/dmtask'
import { AccountGroupApi } from '@/api/facebook/accountgroup'
import { FbAccountApi } from '@/api/facebook/account'
import { DailyLimitApi } from '@/api/facebook/dailylimit'
import UserSelector from '../collect/components/UserSelector.vue'
import ScriptSelector from './ScriptSelector.vue'
import { useMessage } from '@/hooks/web/useMessage'

const message = useMessage()
const dialogVisible = ref(false)
const dialogTitle = ref('')
const formLoading = ref(false)
const formType = ref('')
const formRef = ref()

// 用户选择器
const userSelectorVisible = ref(false)
const selectedUsers = ref<any[]>([])

// 话术选择器
const scriptSelectorVisible = ref(false)
const selectedScripts = ref<string[]>([])
const manualScripts = ref('')
const scriptType = ref(1)

// 账号分组
const accountGroups = ref<any[]>([])
const accounts = ref<any[]>([])

// 间隔范围
const intervalRange = ref('4-10')

const formData = ref({
  id: undefined,
  taskName: '',
  targetUserIds: [] as string[],
  scripts: [] as string[],
  scriptType: 1,
  appendRandomEmoji: false,
  accountIds: [] as string[],
  minIntervalSeconds: 4,
  maxIntervalSeconds: 10,
  remark: ''
})

const formRules = reactive({
  taskName: [{ required: true, message: '任务名称不能为空', trigger: 'blur' }],
  targetUserIds: [
    { 
      required: true, 
      message: '请选择目标用户', 
      trigger: 'change',
      validator: () => selectedUsers.value.length === 0 ? new Error('请选择目标用户') : undefined
    }
  ],
  scripts: [
    {
      required: true,
      message: '请输入或选择话术',
      trigger: 'change',
      validator: () => {
        if (scriptType.value === 1 && !manualScripts.value.trim()) {
          return new Error('请输入话术')
        }
        if (scriptType.value === 2 && selectedScripts.value.length === 0) {
          return new Error('请选择话术')
        }
        return undefined
      }
    }
  ],
  accountIds: [{ required: true, message: '请选择执行账号', trigger: 'change' }]
})

// 过滤账号（只显示选中分组的账号）
const filteredAccounts = computed(() => {
  return accounts.value.filter(acc => 
    formData.value.accountIds.includes(String(acc.id)) || 
    !formData.value.accountIds.length
  )
})

/** 打开弹窗 */
const open = async (type: string, id?: number) => {
  dialogVisible.value = true
  dialogTitle.value = type === 'create' ? '新建群发私信任务' : '修改群发私信任务'
  formType.value = type
  resetForm()
  
  // 加载账号分组和账号列表
  await loadAccountGroups()
  await loadAccounts()
  
  if (id) {
    formLoading.value = true
    try {
      const data = await DmTaskApi.getDmTask(id)
      formData.value = data
      scriptType.value = data.scriptType
      selectedUsers.value = data.targetUserIds.map((fbUserId: string) => ({
        fbUserId,
        userName: fbUserId
      }))
      selectedScripts.value = data.scripts
      manualScripts.value = data.scripts.join('\n')
      
      // 解析间隔
      if (data.minIntervalSeconds === 2 && data.maxIntervalSeconds === 4) {
        intervalRange.value = '2-4'
      } else if (data.minIntervalSeconds === 10 && data.maxIntervalSeconds === 16) {
        intervalRange.value = '10-16'
      } else {
        intervalRange.value = '4-10'
      }
    } finally {
      formLoading.value = false
    }
  }
}

/** 加载账号分组 */
const loadAccountGroups = async () => {
  try {
    accountGroups.value = await AccountGroupApi.getAllEnabledGroups()
  } catch (error) {
    console.error('加载账号分组失败:', error)
  }
}

/** 加载账号列表 */
const loadAccounts = async () => {
  try {
    const res = await FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 1000 })
    accounts.value = res.list || []
    
    // 获取每个账号的剩余次数
    for (const account of accounts.value) {
      try {
        const remaining = await DailyLimitApi.getRemainingCount(String(account.id), 'dm')
        account.remainingDm = remaining
      } catch {
        account.remainingDm = 0
      }
    }
  } catch (error) {
    console.error('加载账号列表失败:', error)
  }
}

/** 打开用户选择器 */
const openUserSelector = () => {
  userSelectorVisible.value = true
}

/** 确认用户选择 */
const handleUserConfirm = (users: any[]) => {
  selectedUsers.value = users
  formData.value.targetUserIds = users.map(u => u.fbUserId)
}

/** 移除选中的用户 */
const removeSelectedUser = (fbUserId: string) => {
  const index = selectedUsers.value.findIndex(u => u.fbUserId === fbUserId)
  if (index > -1) {
    selectedUsers.value.splice(index, 1)
    formData.value.targetUserIds.splice(index, 1)
  }
}

/** 打开话术选择器 */
const openScriptSelector = () => {
  scriptSelectorVisible.value = true
}

/** 确认话术选择 */
const handleScriptConfirm = (script: any) => {
  // 多选模式，添加到列表
  if (!selectedScripts.value.includes(script.scriptContent)) {
    selectedScripts.value.push(script.scriptContent)
  }
}

/** 移除选中的话术 */
const removeSelectedScript = (index: number) => {
  selectedScripts.value.splice(index, 1)
}

/** 提交表单 */
const submitForm = async () => {
  await formRef.value.validate()
  formLoading.value = true
  try {
    // 处理话术
    if (scriptType.value === 1) {
      formData.value.scripts = manualScripts.value.split('\n').filter(s => s.trim())
    } else {
      formData.value.scripts = selectedScripts.value
    }
    formData.value.scriptType = scriptType.value
    
    // 处理间隔
    const [min, max] = intervalRange.value.split('-').map(Number)
    formData.value.minIntervalSeconds = min
    formData.value.maxIntervalSeconds = max
    
    const data = formData.value as any
    if (formType.value === 'create') {
      await DmTaskApi.createDmTask(data)
      message.success('创建成功')
    } else {
      await DmTaskApi.updateDmTask(data)
      message.success('修改成功')
    }
    dialogVisible.value = false
    emit('success')
  } catch (error) {
    console.error('提交失败:', error)
  } finally {
    formLoading.value = false
  }
}

/** 重置表单 */
const resetForm = () => {
  formData.value = {
    id: undefined,
    taskName: '',
    targetUserIds: [],
    scripts: [],
    scriptType: 1,
    appendRandomEmoji: false,
    accountIds: [],
    minIntervalSeconds: 4,
    maxIntervalSeconds: 10,
    remark: ''
  }
  selectedUsers.value = []
  selectedScripts.value = []
  manualScripts.value = ''
  scriptType.value = 1
  intervalRange.value = '4-10'
  formRef.value?.resetFields()
}

defineExpose({ open })

const emit = defineEmits(['success'])
</script>
