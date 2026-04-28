<template>
  <Dialog v-model="visible" title="选择话术" width="800px">
    <div v-loading="loading">
      <!-- 搜索栏 -->
      <el-form :inline="true" class="mb-10px">
        <el-form-item label="话术标题">
          <el-input
            v-model="searchKeyword"
            placeholder="请输入话术标题"
            clearable
            @keyup.enter="loadScripts"
          />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="loadScripts">搜索</el-button>
        </el-form-item>
      </el-form>

      <!-- 话术列表 -->
      <el-table
        ref="tableRef"
        :data="scriptList"
        stripe
        border
        max-height="400"
        highlight-current-row
        @current-change="handleCurrentChange"
      >
        <el-table-column label="选择" width="60" align="center">
          <template #default="{ row }">
            <el-radio
              v-model="selectedScriptId"
              :label="row.id"
              @change="handleSelectScript(row)"
            >
              &nbsp;
            </el-radio>
          </template>
        </el-table-column>
        <el-table-column label="标题" prop="scriptTitle" min-width="120" show-overflow-tooltip />
        <el-table-column label="内容" prop="scriptContent" min-width="300" show-overflow-tooltip>
          <template #default="{ row }">
            {{ row.scriptContent }}
          </template>
        </el-table-column>
      </el-table>

      <!-- 分页 -->
      <el-pagination
        v-if="total > 0"
        class="mt-4"
        v-model:current-page="queryParams.pageNo"
        v-model:page-size="queryParams.pageSize"
        :total="total"
        :page-sizes="[10, 20, 50, 100]"
        layout="total, sizes, prev, pager, next, jumper"
        @size-change="loadScripts"
        @current-change="loadScripts"
      />
    </div>

    <template #footer>
      <el-button type="primary" @click="handleConfirm">确 定</el-button>
      <el-button @click="visible = false">取 消</el-button>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { ScriptApi, FbScriptVO } from '@/api/facebook/script'

const props = defineProps<{
  modelValue: boolean
}>()

const emit = defineEmits(['update:modelValue', 'confirm'])

const visible = ref(false)
const loading = ref(false)
const tableRef = ref()
const searchKeyword = ref('')
const scriptList = ref<FbScriptVO[]>([])
const total = ref(0)
const selectedScript = ref<FbScriptVO | null>(null)
const selectedScriptId = ref<number | undefined>(undefined)

// 查询参数
const queryParams = ref({
  pageNo: 1,
  pageSize: 10,
  scriptTitle: ''
})

// 监听modelValue变化
watch(() => props.modelValue, (val) => {
  visible.value = val
  if (val) {
    loadScripts()
  }
})

// 监听visible变化
watch(visible, (val) => {
  emit('update:modelValue', val)
})

/** 加载话术列表 */
const loadScripts = async () => {
  loading.value = true
  try {
    queryParams.value.scriptTitle = searchKeyword.value
    const data = await ScriptApi.getScriptPage(queryParams.value)
    scriptList.value = data.list || []
    total.value = data.total || 0
  } catch (error) {
    console.error('加载话术列表失败:', error)
    scriptList.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

/** 行选中变化 */
const handleCurrentChange = (row: FbScriptVO | null) => {
  if (row) {
    handleSelectScript(row)
  }
}

/** 选择话术 */
const handleSelectScript = (row: FbScriptVO) => {
  selectedScript.value = row
  selectedScriptId.value = row.id
}

/** 确认选择 */
const handleConfirm = () => {
  if (selectedScript.value) {
    emit('confirm', selectedScript.value)
    visible.value = false
  }
}
</script>
