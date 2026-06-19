import type { InjectionKey } from 'vue'
import type { DragRescheduleContext } from './useDragReschedule'

export const DRAG_RESCHEDULE_KEY = Symbol() as InjectionKey<DragRescheduleContext>
