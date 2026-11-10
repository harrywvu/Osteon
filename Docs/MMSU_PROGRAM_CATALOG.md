# MMSU program catalog review

The account API serves a local catalog of 52 program IDs spanning agriculture,
aquatic sciences, arts and sciences, computing, business, engineering, health,
industrial technology, teacher education, veterinary medicine, dentistry,
medicine, and law. Program IDs are stable data values; names are display text.
Learners supply these details themselves. They are not verified against MMSU
enrollment records.

This is a **release review item**. The current MMSU home page says there are
[49 undergraduate degree programs](https://www.mmsu.edu.ph/?redirect=0), while
this catalog includes professional degrees and some offerings from the
[MMSU faculty manual](https://www.mmsu.edu.ph/resources/1oHHscTsr6T_QB89TYzUQm4Ldzzj0q33O/data).
Confirm which older entries are still offered and reconcile the undergraduate
count before deploying mandatory program selection. The current
[CCIS page](https://www.mmsu.edu.ph/page/245/1/college-of-computing-and-information-sciences)
confirms BS Computer Science and BS Information Technology. The
[Health Sciences page](https://chs.mmsu.edu.ph/article/accreditation-level)
confirms Nursing, Pharmacy, Physical Therapy, and Radiologic Technology.
MMSU describes [Veterinary Medicine as six years](https://www.mmsu.edu.ph/article/news/2961/seasoned-vets-examine-animal-health-in-mmsu-lectures)
and [Dental Medicine as six years](https://www.mmsu.edu.ph/resources/12PC11Q4E2yDBuJDfMrXUmSrB031Ab4tK/data).

The current API offers year levels 1–6 for every program so it does not reject
students in a longer degree while individual curriculum lengths are checked.
Narrow each program's `year_options` after checking its current official
curriculum. This conservative range is a temporary local implementation limit,
not a claim that every degree lasts six years.
