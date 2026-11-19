"use strict";
const config=window.OSTEON_EDUCATOR_CONFIG;
const $=id=>document.getElementById(id);
const views=["welcome","questions","learners"];
const titles={welcome:"Osteon Educator",questions:"Questions · Osteon Educator",learners:"Learners · Osteon Educator"};
const editFields=["bone_group","bone_name","difficulty_level","difficulty_score","question_text","option_a","option_b","option_c","option_d","correct_option"];
const counters={question_text:180,option_a:70,option_b:70,option_c:70,option_d:70};
let accessToken=null,expiresAt=0,bank=[],baseVersion=null,selected=null,dirty=false,cursor=null,publicationId=null,programNames=new Map(),currentView=null,lastLearnerButton=null;

/* Messages: one dismissible region, announced politely for status and assertively for errors. */
function notice(message,kind="info"){
  const wrap=$("notice-wrap"),node=$("notice");
  node.setAttribute("role",kind==="error"?"alert":"status");
  node.setAttribute("aria-live",kind==="error"?"assertive":"polite");
  node.textContent=message||"";
  wrap.hidden=!message;
  if(message)wrap.dataset.kind=kind;else delete wrap.dataset.kind;
}
const setBusy=(node,busy)=>{if(node)node.toggleAttribute("aria-busy",Boolean(busy));};
async function run(button,label,task){
  const original=button?button.textContent:null;
  if(button){button.disabled=true;button.textContent=label;}
  try{return await task();}
  finally{if(button){button.textContent=original;button.disabled=false;}}
}

/* Views, tabs, and focus. */
function show(view){
  if(view===currentView)return;
  for(const id of views)$(id).hidden=id!==view;
  currentView=view;
  document.title=titles[view];
  $("app-nav").hidden=!accessToken;
  $("questions-tab").setAttribute("aria-current",view==="questions"?"page":"false");
  $("learners-tab").setAttribute("aria-current",view==="learners"?"page":"false");
  notice("");
  const heading=$(view).querySelector("h2");
  if(heading)heading.focus({preventScroll:true});
  window.scrollTo(0,0);
}

/* Session and API. */
function session(){if(!accessToken||Date.now()>=expiresAt){accessToken=null;show("welcome");throw Error("Your educator session expired. Sign in again.");}}
async function api(path,options={}){
  session();
  const response=await fetch(config.apiBase+path,{...options,headers:{Authorization:"Bearer "+accessToken,...(options.body?{"Content-Type":"application/json"}:{})},cache:"no-store"});
  const data=await response.json().catch(()=>({}));
  if(response.status===401||response.status===403){accessToken=null;show("welcome");throw Error("Educator access ended. Sign in again.");}
  if(!response.ok){const error=Error(data.message||data.error||"Request failed");error.status=response.status;throw error;}
  return data;
}
const encode=bytes=>btoa(String.fromCharCode(...bytes)).replace(/\+/g,"-").replace(/\//g,"_").replace(/=+$/g,"");
function randomString(){return encode(crypto.getRandomValues(new Uint8Array(32)));}
async function login(endpoint="/oauth2/authorize"){
  const verifier=randomString(),state=randomString(),digest=await crypto.subtle.digest("SHA-256",new TextEncoder().encode(verifier));
  sessionStorage.setItem("osteon-pkce",JSON.stringify({verifier,state}));
  const url=new URL(config.cognitoDomain+endpoint);
  url.search=new URLSearchParams({response_type:"code",client_id:config.clientId,redirect_uri:config.redirectUri,scope:"openid",state,code_challenge:encode(new Uint8Array(digest)),code_challenge_method:"S256"});
  location.assign(url);
}
async function callback(){
  const url=new URL(location.href),code=url.searchParams.get("code");
  if(!code){if(url.searchParams.has("error"))notice("Sign-in was cancelled or failed.","error");return;}
  const pending=JSON.parse(sessionStorage.getItem("osteon-pkce")||"null");
  sessionStorage.removeItem("osteon-pkce");history.replaceState({},"",config.redirectUri);
  if(!pending||pending.state!==url.searchParams.get("state"))throw Error("Sign-in state did not match. Try again.");
  const response=await fetch(config.cognitoDomain+"/oauth2/token",{method:"POST",headers:{"Content-Type":"application/x-www-form-urlencoded"},body:new URLSearchParams({grant_type:"authorization_code",client_id:config.clientId,code,redirect_uri:config.redirectUri,code_verifier:pending.verifier})});
  const token=await response.json();if(!response.ok||!token.access_token)throw Error("Sign-in could not be completed.");
  accessToken=token.access_token;expiresAt=Date.now()+Math.min(Number(token.expires_in)||900,900)*1000;
  show("questions");
  await loadQuestions();
}

/* Questions. */
const editorHasChanges=()=>selected!==null&&editFields.some(field=>$("editor").elements[field].value.trim()!==String(bank[selected][field]??""));
const confirmDiscard=message=>!(dirty||editorHasChanges())||confirm(message);
function toggleEditor(on){$("editor").hidden=!on;$("editor-empty").hidden=on;}
function updateCounters(){
  const form=$("editor");
  for(const [field,max] of Object.entries(counters)){
    const input=form.elements[field],output=document.querySelector(`[data-count-for="${field}"]`);
    if(!input||!output)continue;
    const length=input.value.length;
    output.textContent=`${length}/${max}`;
    output.classList.toggle("near-limit",length>=max);
  }
}
function updateAnswerHint(){
  const form=$("editor");
  if(form.hidden)return;
  const option=form.elements.correct_option.value.toLowerCase(),text=form.elements["option_"+option]?.value.trim();
  $("answer-hint").textContent=text?`Choice ${option.toUpperCase()} reads "${text}".`:`Choice ${option.toUpperCase()} is empty.`;
}
function renderQuestions(){
  const list=$("question-list"),term=$("question-search").value.trim().toLocaleLowerCase(),scrollTop=list.scrollTop;
  list.replaceChildren();
  let shown=0;
  bank.forEach((row,index)=>{
    if(term&&!`${row.question_id} ${row.question_text} ${row.bone_name}`.toLocaleLowerCase().includes(term))return;
    const button=document.createElement("button");
    button.type="button";
    button.textContent=`${row.question_id}: ${row.question_text}`;
    if(selected===index)button.setAttribute("aria-current","true");
    button.onclick=()=>selectQuestion(index);
    list.append(button);
    shown++;
  });
  if(!shown){
    const empty=document.createElement("p");
    empty.className="empty";
    empty.textContent=bank.length?"No questions match that search.":"The question bank is empty.";
    list.append(empty);
  }
  list.scrollTop=scrollTop;
  $("version").textContent=baseVersion?`Published bank: ${baseVersion}`:"Published bank unavailable";
  $("question-count").textContent=term?`${shown} of ${bank.length} questions`:`${bank.length} question${bank.length===1?"":"s"}`;
  $("draft-state").textContent=dirty?"Unpublished changes in this browser":"No unpublished changes";
  $("publish").disabled=!dirty;
}
function selectQuestion(index){
  if(index===selected)return;
  if(editorHasChanges()&&!confirm("Discard unsaved edits to this question?"))return;
  selected=index;
  const form=$("editor"),row=bank[index];
  for(const field of editFields)form.elements[field].value=row[field];
  $("editing").textContent=row.question_id;
  toggleEditor(true);
  updateCounters();updateAnswerHint();renderQuestions();
  $("editing").focus({preventScroll:true});
}
function saveDraft(event){
  event.preventDefault();
  if(selected===null)return;
  const form=$("editor");
  let valid=true;
  for(const field of editFields){
    const input=form.elements[field],value=input.value.trim();
    input.setCustomValidity(value?"":"This field is required.");
    if(!value)valid=false;
  }
  if(!valid){form.reportValidity();notice("Fill in every field before saving the draft.","error");return;}
  const row=bank[selected];
  for(const field of editFields)row[field]=form.elements[field].value.trim();
  dirty=true;publicationId=null;
  renderQuestions();updateAnswerHint();
  notice("Draft saved in this browser. Publish to make it live for new quizzes.","success");
}
async function loadQuestions(){
  setBusy($("questions"),true);
  $("version").textContent="Loading published bank…";
  try{
    const data=await api("/v1/questions");
    bank=data.questions;baseVersion=data.version;selected=null;dirty=false;publicationId=null;
    toggleEditor(false);renderQuestions();
  }finally{setBusy($("questions"),false);}
}
async function openQuestions(){
  if(!accessToken)return show("welcome");
  show("questions");
  if(dirty&&bank.length){renderQuestions();return;}
  try{await loadQuestions();}catch(error){notice(error.message,"error");}
}
async function publish(){
  if(!dirty)return;
  if(editorHasChanges()){notice("Save this question draft before publishing.","error");return;}
  if(!confirm(`Publish ${bank.length} question${bank.length===1?"":"s"}? This replaces the live bank for new quiz sessions.`))return;
  publicationId??=crypto.randomUUID();
  const button=$("publish"),label=button.textContent;
  button.textContent="Publishing…";button.disabled=true;
  try{
    const data=await api("/v1/questions/publish",{method:"POST",body:JSON.stringify({base_version:baseVersion,questions:bank,request_id:publicationId})});
    notice("Published bank "+data.version+".","success");
    await loadQuestions();
  }catch(error){
    notice(error.status===409?"Another educator published first. Your edits are still in this browser; copy anything you need before reloading the published bank.":error.message,"error");
  }finally{button.textContent=label;button.disabled=!dirty;}
}
function element(tag,text){const node=document.createElement(tag);node.textContent=String(text??"");return node;}
function metric(label,value){const card=element("div","");card.className="metric";card.append(element("span",label),element("strong",value));return card;}
function breakdown(title,items){
  const section=element("section","");section.className="analysis-block";section.append(element("h4",title));
  if(!items.length){section.append(element("p","No categorized answers in the retained window."));return section;}
  for(const item of items){
    const row=element("div","");row.className="breakdown-row";
    row.append(element("span",`${item.label}: ${item.correct}/${item.answered} correct`));
    const track=element("div","");track.className="bar-track";track.setAttribute("aria-hidden","true");
    const fill=element("div","");fill.className="bar-fill";fill.style.width=`${100*item.correct/item.answered}%`;
    track.append(fill);row.append(track);section.append(row);
  }
  return section;
}
function masteryChart(estimates){
  const section=element("section","");section.className="analysis-block";section.append(element("h4","Mastery estimate across retained answers"));
  if(estimates.length<2){section.append(element("p","A mastery trace is unavailable for these retained answers."));return section;}
  const delta=Math.round((estimates.at(-1)-estimates[0])*100);
  section.append(element("p",`${Math.round(estimates[0]*100)}% → ${Math.round(estimates.at(-1)*100)}% (${delta>0?"+":""}${delta} percentage points). This is a model estimate, not a grade.`));
  const svg=document.createElementNS("http://www.w3.org/2000/svg","svg");svg.setAttribute("viewBox","0 0 640 150");svg.setAttribute("role","img");svg.setAttribute("aria-label","Mastery estimate after each retained answer");svg.classList.add("mastery-chart");
  const points=estimates.map((value,index)=>`${20+600*index/(estimates.length-1)},${130-110*value}`).join(" ");
  const line=document.createElementNS("http://www.w3.org/2000/svg","polyline");line.setAttribute("points",points);line.setAttribute("fill","none");line.setAttribute("stroke","#075b9c");line.setAttribute("stroke-width","4");svg.append(line);
  estimates.forEach((value,index)=>{const dot=document.createElementNS("http://www.w3.org/2000/svg","circle");dot.setAttribute("cx",String(20+600*index/(estimates.length-1)));dot.setAttribute("cy",String(130-110*value));dot.setAttribute("r","5");dot.setAttribute("fill","#075b9c");svg.append(dot);});
  section.append(svg);return section;
}

/* Learners. */
const learnerName=profile=>[profile.first_name,profile.middle_name,profile.last_name].filter(Boolean).join(" ")||profile.profile_label||"Incomplete profile";
async function loadLearners(next=false){
  const list=$("learner-list"),status=$("learner-status"),term=$("learner-search").value.trim();
  if(!next){cursor=null;list.replaceChildren();$("learner-detail").hidden=true;lastLearnerButton=null;status.textContent="Loading learners…";}
  setBusy($("learners"),true);
  try{
    if(!programNames.size){const catalog=await api("/v1/programs");programNames=new Map(catalog.programs.map(item=>[item.id,item.name]));}
    const query=new URLSearchParams({q:term});if(cursor)query.set("cursor",cursor);
    const data=await api("/v1/learners?"+query);cursor=data.next_cursor;$("more").hidden=!cursor;
    for(const item of data.learners){
      const p=item.profile,s=item.progress,card=element("div","");card.className="card";
      const button=element("button",learnerName(p));button.type="button";
      button.onclick=()=>loadDetail(p.learner_id,button).catch(error=>notice(error.message,"error"));
      card.append(button);
      card.append(element("span",` ${programNames.get(p.program_id)||p.program_id||"Program incomplete"} · Year ${p.year_level||"incomplete"} · ${s?Math.round(s.mastery_estimate*100)+"% model estimate, "+s.answered_count+" answers this run":"No quiz started"}`));
      list.append(card);
    }
    if(!data.learners.length&&!next){
      const empty=element("p",term?"No learners match that name. Clear the search or try a different spelling.":"No learners have created an account yet.");
      empty.className="empty";list.append(empty);
    }
    const total=list.querySelectorAll(".card").length;
    status.textContent=!total?(term?"No matches.":"No learners yet."):`${next?`${total} learners shown so far`:total===1?"1 learner found":`${total} learners found`}.`;
  }finally{setBusy($("learners"),false);}
}
async function loadDetail(id,button){
  const data=await api("/v1/learners/"+id),p=data.profile,s=data.progress,a=data.analysis,article=$("learner-detail");
  article.replaceChildren();article.hidden=false;
  const head=element("div","");head.className="detail-head";
  const title=element("h3",learnerName(p));title.tabIndex=-1;
  const close=element("button","Close");close.type="button";close.className="secondary";
  close.onclick=()=>{article.hidden=true;if(lastLearnerButton)lastLearnerButton.focus();};
  head.append(title,close);article.append(head);
  if(button){lastLearnerButton=button;button.setAttribute("aria-expanded","true");}
  const dl=document.createElement("dl");
  for(const [key,value] of [["Learner ID",p.learner_id],["Program",programNames.get(p.program_id)||p.program_id||"Incomplete"],["Year",p.year_level||"Incomplete"],["Quiz status",s?(s.completed?"Completed":"In progress"):"Not started"],["Current question",s?.current_question_id||"None"]])dl.append(element("dt",key),element("dd",value));
  article.append(dl);
  const metrics=element("div","");metrics.className="metrics";
  metrics.append(metric("Mastery estimate",s?Math.round(s.mastery_estimate*100)+"%":"No quiz"),
    metric("Answers this run",s?.answered_count??0),
    metric("Retained answers",a.retained_count),
    metric("Correct in retained window",a.graded_count?`${a.correct_count}/${a.graded_count} (${Math.round(100*a.correct_count/a.graded_count)}%)`:"No graded answers"));
  article.append(metrics,element("p","Analysis below uses only the most recent retained answers (up to 10). It may include earlier quiz runs. It is not a lifetime score."));
  article.append(masteryChart(a.mastery_estimates),breakdown("By bone group",a.by_group),breakdown("By difficulty",a.by_difficulty));
  article.append(element("h4","Recent answers"));
  if(!data.recent_answers.length)article.append(element("p","No answers retained."));
  for(const answer of [...data.recent_answers].reverse()){
    const row=element("div","");row.className="answer-row";
    row.append(element("strong",answer.question_text||answer.question_id||"Question unavailable"));
    const descriptors=[answer.bone_name,answer.bone_group,answer.difficulty].filter(Boolean);
    if(answer.accepted_at&&Number.isFinite(Number(answer.accepted_at)))descriptors.push(new Date(Number(answer.accepted_at)*1000).toLocaleString());
    if(descriptors.length)row.append(element("p",descriptors.join(" · ")));
    const chosen=`Choice ${answer.choice||"unknown"}${answer.chosen_text?" — "+answer.chosen_text:""}`;
    const outcome=answer.correct===true?"Correct":answer.correct===false?"Incorrect":"Outcome unavailable";
    row.append(element("p",`${chosen} · ${outcome}`));
    if(answer.correct===false&&answer.correct_option)row.append(element("p",`Correct answer: ${answer.correct_option}${answer.correct_text?" — "+answer.correct_text:""}`));
    if(typeof answer.previous_mastery==="number"&&typeof answer.updated_mastery==="number")row.append(element("p",`Model estimate: ${Math.round(answer.previous_mastery*100)}% → ${Math.round(answer.updated_mastery*100)}%`));
    if(!answer.question_text)row.append(element("p","Original question text unavailable for this legacy answer."));
    article.append(row);
  }
  title.focus({preventScroll:true});
}
async function openLearners(){
  if(!accessToken)return show("welcome");
  show("learners");
  if($("learner-list").children.length)return;
  try{await loadLearners();}catch(error){notice(error.message,"error");}
}

/* Wiring. */
window.addEventListener("beforeunload",event=>{if(dirty||editorHasChanges()){event.preventDefault();event.returnValue="";}});
$("login").onclick=()=>login().catch(error=>notice(error.message,"error"));
$("signup").onclick=()=>login("/signup").catch(error=>notice(error.message,"error"));
$("notice-close").onclick=()=>notice("");
$("logout").onclick=()=>{
  if(!confirmDiscard("Sign out and discard your unpublished question changes?"))return;
  accessToken=null;expiresAt=0;dirty=false;sessionStorage.removeItem("osteon-pkce");
  const url=new URL(config.cognitoDomain+"/logout");url.search=new URLSearchParams({client_id:config.clientId,logout_uri:config.redirectUri});
  location.assign(url);
};
$("questions-tab").onclick=()=>openQuestions();
$("learners-tab").onclick=()=>openLearners();
$("editor").onsubmit=saveDraft;
$("editor").addEventListener("input",event=>{if(event.target.setCustomValidity)event.target.setCustomValidity("");updateCounters();updateAnswerHint();});
$("revert").onclick=()=>{
  if(selected===null)return;
  const form=$("editor"),row=bank[selected];
  for(const field of editFields)form.elements[field].value=row[field];
  updateCounters();updateAnswerHint();
  notice("Question restored to the last saved version in this browser.");
};
$("question-search").oninput=renderQuestions;
$("publish").onclick=()=>publish().catch(error=>notice(error.message,"error"));
$("reload").onclick=()=>run($("reload"),"Reloading…",async()=>{
  if(!confirmDiscard("Reload the published bank and discard your unpublished changes?"))return;
  try{await loadQuestions();notice("Loaded published bank "+baseVersion+".","success");}
  catch(error){notice(error.message,"error");}
});
$("learner-search-form").onsubmit=event=>{event.preventDefault();run($("learner-find"),"Searching…",()=>loadLearners()).catch(error=>notice(error.message,"error"));};
$("more").onclick=()=>run($("more"),"Loading…",()=>loadLearners(true)).catch(error=>notice(error.message,"error"));
document.addEventListener("keydown",event=>{
  if(!(event.ctrlKey||event.metaKey)||event.altKey||event.key.toLowerCase()!=="s")return;
  if($("editor").hidden)return;
  event.preventDefault();
  $("editor").requestSubmit();
});
if(config?.apiBase&&config?.cognitoDomain&&config?.clientId&&config?.redirectUri)callback().catch(error=>notice(error.message,"error"));
else notice("Portal configuration is missing.","error");
